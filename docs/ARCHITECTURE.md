# Tower Defense — Architecture Overview

Last reviewed: June 2026.

This document describes the current state of the Unity Tower Defense project. It is the
single source of truth for the high-level structure of the codebase. Any code change
that adds, removes, or alters a public API, a subsystem, a scene boundary, or a runtime
flow **must** update this document in the same change. See
[CODING_GUIDELINES.md](../CODING_GUIDELINES.md#documentation-maintenance).

---

## 1. Repository Layout

```
TD/
├── Assets/
│   ├── Editor/                 # Editor-only tooling (TD.Editor asmdef)
│   ├── Prefabs/
│   ├── Scenes/                 # Boot, Menu, Gameplay
│   ├── ScriptableObjects/      # Tower/Enemy/Bullet/Level/Menu data assets
│   ├── Scripts/                # Runtime gameplay code (TD.Runtime asmdef)
│   ├── Sprites/
│   └── Tests/
│       └── EditMode/           # NUnit EditMode tests (TD.Tests.EditMode asmdef)
├── Packages/
├── ProjectSettings/
├── docs/                       # Architecture & design notes (this folder)
├── tools/
├── CODING_GUIDELINES.md
├── README.md
└── TD.slnx
```

`Assets/Scripts/` is grouped by subsystem; one folder per subsystem, no namespaces yet
(default namespace is used everywhere). Three assembly definitions split runtime,
editor, and test code so the test assembly can reference runtime code without leaking
editor-only types.

---

## 2. Subsystems

30 C# files across 10 subsystems (28 runtime + 2 editor).

| Subsystem    | Folder                          | Files                                                                                          |
| ------------ | ------------------------------- | ---------------------------------------------------------------------------------------------- |
| Core         | `Assets/Scripts/Core/`          | `GameSession`, `GameState`, `BuildManager`, `Difficulty`, `PrefabPool`                         |
| Grid         | `Assets/Scripts/Grid/`          | `GridManager`, `GridCell`                                                                      |
| Pathfinding  | `Assets/Scripts/Pathfinding/`   | `Pathfinder` (BFS)                                                                             |
| Enemy        | `Assets/Scripts/Enemy/`         | `Enemy`, `EnemyData`, `EnemySpawner`                                                           |
| Towers       | `Assets/Scripts/Towers/`        | `Tower`, `TowerData`, `TowerUpgradeData`, `RangeIndicator`, `TowerSelectionController`         |
| Bullets      | `Assets/Scripts/Bullets/`       | `Bullet`, `BulletData`                                                                         |
| Level        | `Assets/Scripts/Level/`         | `LevelData`, `LevelMapDefinition`, `LevelLoader`, `MapCameraController`, `MapCameraFramer`     |
| UI           | `Assets/Scripts/UI/`            | `InGameHudController`, `RuntimeMapEditorController`                                            |
| Menu         | `Assets/Scripts/Menu/`          | `TitleScreenController`, `MainMenuController`, `MainMenuConfig`                                |
| Editor       | `Assets/Editor/`                | `LevelMapEditorWindow`, `ProjectStructureExporter`                                             |
| Utilities    | `Assets/Scripts/Utilities/`     | (currently empty — reserved for cross-cutting helpers)                                         |

---

## 3. Scenes & Runtime Flow

Three scenes drive the game:

| Scene                                             | Purpose                                                |
| ------------------------------------------------- | ------------------------------------------------------ |
| [Boot.unity](../Assets/Scenes/Boot.unity)         | Minimal entry point; bootstraps persistent services    |
| [Menu.unity](../Assets/Scenes/Menu.unity)         | Title screen + main menu, no gameplay state            |
| [Gameplay.unity](../Assets/Scenes/Gameplay.unity) | Grid, level, towers, enemies, HUD, runtime map editor  |

```
Boot ──▶ TitleScreenController ──▶ Menu
                                    │
                                    ├─ MainMenuController
                                    │   ├─ Campaign  ─▶ GameSession.SelectLevel()         ─▶ Gameplay
                                    │   ├─ Custom    ─▶ GameSession.SelectMapSeed()       ─▶ Gameplay
                                    │   └─ Editor    ─▶ GameSession.BeginMapEditorMode()  ─▶ Gameplay
                                    │
                                    └─ RuntimeMapEditorController
                                        ├─ Paint path / blocked / start / goal
                                        ├─ Save Custom Map (PlayerPrefs)
                                        └─ Test Run ─▶ Gameplay (round 1 with current seed)
Gameplay
  LevelLoader ─▶ GridManager ─▶ Pathfinder
  EnemySpawner ─▶ Enemy.ActiveEnemies
  BuildManager ─▶ Tower (Update → FindNearestEnemy → Shoot ─▶ Bullet via PrefabPool)
  TowerSelectionController ─▶ RangeIndicator + InGameHudController upgrade/sell panel
  GameState (round, money) ◀▶ InGameHudController
```

Cross-scene state lives on `GameSession` (static) — selected level, map seed, difficulty,
editor flags. Runtime state lives on `GameState` (singleton MonoBehaviour) — current round
and money.

---

## 4. Data Model

All gameplay data is authored as ScriptableObjects under `Assets/ScriptableObjects/`:

| Asset type           | Defines                                                           |
| -------------------- | ----------------------------------------------------------------- |
| `TowerData`          | Tower base stats, prefab, default `BulletData`                    |
| `TowerUpgradeData`   | Per-tower upgrade ladder (cost + bonuses + bullet override)       |
| `EnemyData`          | Enemy stats (health, speed, shield, armor, reward)                |
| `BulletData`         | Projectile behaviour (speed, splash, slow, piercing, prefab)      |
| `LevelData`          | Grid size, start/goal, blocked cells, path cells, optional seed   |
| `MainMenuConfig`     | Main-menu layout (buttons, campaign list, titles)                 |

Maps support two interchangeable representations:

- **Explicit cells** — `LevelData.startCell`, `goalCell`, `blockedCells`, `pathCells`.
- **Seed string** — JSON encoded via `LevelMapSeedUtility`, decoded into a
  `LevelMapDefinition` and applied with `LevelData.ApplyDefinition()`.

Custom maps created at runtime are persisted via `CustomMapStorage` (PlayerPrefs).

---

## 5. Public API Quick Reference

This is the contract the rest of the codebase relies on. **Update this section whenever a
public method or property is added, removed, or renamed.**

### Core
- `GameSession` (static) — `SelectLevel`, `SelectMapSeed`, `SelectMapDefinition`,
  `ClearSelectedLevel`, `BeginTestRun`, `EndTestRun`, `SelectDifficulty`,
  `BeginMapEditorMode`, `EndMapEditorMode`; properties `SelectedLevelData`,
  `SelectedMapDefinition`, `SelectedMapSeed`, `IsEditorTestRun`, `SelectedDifficulty`,
  `IsMapEditorSession`.
- `GameState` — `Instance`, `CurrentRound`, `Money`; events `OnRoundChanged`,
  `OnMoneyChanged`; methods `GetOrCreate`, `ResetState`, `SetRound`, `AddMoney`,
  `SpendMoney`, `ResetMoney`.
- `BuildManager` — `IsPlacingTower`, `SelectedTowerToBuild`, `AvailableTowers`;
  event `OnBuildSelectionChanged`; methods `SelectTowerToBuild`,
  `ClearSelectedTowerToBuild`, `PlaceTower`, `PlaceTowerAt`.
- `Difficulty` — enum `DifficultyLevel { Easy, Normal, Hard, Nightmare }`,
  `DifficultySettings.ForLevel(level)`.
- `PrefabPool` (static) — `Spawn(prefab, position, rotation)`,
  `Release(instance)`, `Clear()`.

### Grid & Pathfinding
- `GridManager` — `BuildGrid`, `BuildGridPreview`, `ClearPreviewVisuals`, `GetCell`,
  `GetNeighbors`, `WorldToCell`, `CellToWorld`, `IsReservedPathCell`,
  `CanEnemyWalkOn`, `GetCachedEnemyPathWorld`, `SetCellOccupied`, `TryReservePathCells`;
  event `OnPathChanged`.
- `GridCell` — `X`, `Y`, `Position`, `IsBlocked`, `IsOccupied`, `IsPath`;
  `IsWalkable`, `SetBlocked`, `SetPath`, `SetOccupied`.
- `Pathfinder` — `FindPath(grid, start, goal)`, `HasPath(grid, start, goal)`.

### Enemy
- `Enemy` — `ActiveEnemies` (static `IReadOnlyList<Enemy>`), `IsDead`, `Data`, `Reward`;
  events `OnDied`, `OnReachedGoal`; methods `Initialize`, `SetWaypoints`, `TakeDamage`,
  `TakeShield`, `ApplySlowEffect`, `Heal`, `Die`.
- `EnemySpawner` — events `OnRoundStarted`, `OnAllEnemiesDefeated`, `OnEnemySpawned`;
  methods `BeginSpawning`, `PauseSpawning`, `ResumeSpawning`, `StopSpawning`.

### Towers & Bullets
- `Tower` — `Data`, `CurrentUpgradeLevel`, `Damage`, `AttackSpeed`, `Range`;
  `Initialize`, `CanUpgrade`, `GetNextUpgradeCost`, `TryUpgrade`, `GetSellValue`.
- `RangeIndicator` — `Show(center, radius)`, `Hide()`.
- `TowerSelectionController` — `Configure`, `SelectTower`, `DeselectTower`,
  `HideRangeIndicator`.
- `Bullet` — `Initialize(bulletData, damage, target)`.

### Level
- `LevelData` — `width`, `height`, `mapSeed`, `startCell`, `goalCell`, `blockedCells`,
  `pathCells`; `GetMapDefinition`, `TryGetMapDefinition`, `ApplyDefinition`.
- `LevelMapDefinition` — `version`, `width`, `height`, `startCell`, `goalCell`,
  `blockedCells`, `pathCells`, `HasExplicitPath`; `FromLegacy`, `CloneNormalized`.
- `LevelLoader` — `DefaultLevelData`, `HasLoadedLevel`; events `OnLevelLoaded`,
  `OnMapLoaded`; methods `LoadSelectedOrDefaultLevel`, `LoadLevel`, `LoadMap`,
  `RefreshCameraFrameForScreenSize`.
- `MapCameraController` — `SetFrame(frame)`.
- `MapCameraFramer` (static) — `Frame(width, height, cellSize, uiReserve)` returning
  `MapCameraFrame`.

### UI & Menu
- `InGameHudController` — `PanelWidth`; event `OnBackToEditorRequested`; methods
  `Show`, `Hide`, `RefreshAll`, `RefreshTopButtons`.
- `RuntimeMapEditorController` — `Open`, `Close`, `SaveCustomMap`, `SaveCampaignSeed`,
  `StartTestRun`.
- `MainMenuController` — `ShowMainMenu`, `HideMenu`, `ShowCampaignPanel`,
  `ShowCustomMapsPanel`, `ShowPlaceholder`.
- `TitleScreenController` — drives splash + transition to Menu (no public API).
- `MainMenuConfig` — `title`, `mainButtons`, `campaignTitle`, `campaignLevels`.

### Editor
- `LevelMapEditorWindow` — menu `Tools/Tower Defense/Map Editor`, `OpenWindow()`.
- `ProjectStructureExporter` — menu `Tools/Export/Export Assets Folder To TXT`.

---

## 6. Conventions in Effect

- **Language**: Code, identifiers, commit messages, PR descriptions in English.
  Some legacy German inline comments still exist in `Tower`, `Enemy`, `PrefabPool`,
  `TitleScreenController` — translate when touching those files.
- **Style & lifecycle**: see [CODING_GUIDELINES.md](../CODING_GUIDELINES.md).
- **No allocations in `Update()`**: enforced for towers (uses `Enemy.ActiveEnemies`,
  squared-distance comparison) and bullets.
- **Object pooling**: bullets and enemies must spawn/despawn via `PrefabPool`.
- **Map seeds**: `mapSeed` is normative; `pathCells` exist only when an explicit path
  was authored. The grid's `UsesExplicitPath` reflects this.

---

## 7. Known Gaps & Suggested Next Steps

These are not blocking, but they are the highest-leverage improvements visible in the
current code.
Namespaces** — everything is in the default namespace. Adopting
   `TD.<Subsystem>` namespaces would prevent collisions and document boundaries.
3. **German → English comment sweep** — small, low-risk, but should be done
   opportunistically when editing each file.
4. **HUD/Menu UI source** — both menus and the HUD are constructed from code at
   runtime. Once the layout stabilizes, migrating to UXML/Prefabs would make iteration
   and theming far cheaper.
5. **Test coverage expansion** — `EnemySpawner` round scaling and `BuildManager`
   placement rules would benefit from PlayMode test
7. **Save/load custom maps** — `CustomMapStorage` uses PlayerPrefs (fine for now),
   but a JSON file under `Application.persistentDataPath` would scale better and
   survive PlayerPrefs corruption.
8. **README** — repo currently has no top-level `README.md`. A short one pointing
   to this document and `CODING_GUIDELINES.md` would help newcomers.

---

## 8. Change Log

Append a one-line entry whenever this document is updated.

- 2026-06-03: Initial architecture snapshot extracted from code review of 30 C# files.
- 2026-06-03: Introduced asmdef structure (`TD.Runtime`, `TD.Editor`, `TD.Tests.EditMode`); added EditMode test suite under `Assets/Tests/EditMode/` covering `Pathfinder`, `LevelMapDefinition`, `LevelMapValidator`, `LevelMapSeedUtility`, `Difficulty`, `MapCameraFrame`. `CustomMapStorage` now persists to `Application.persistentDataPath/customMaps.json` (auto-migrates from legacy PlayerPrefs key).
