# Tower Defense - Architecture Overview

Last reviewed: June 2026.

This document describes the current state of the Unity Tower Defense project. It is the
single source of truth for the high-level structure of the codebase. Any code change
that adds, removes, or alters a public API, a subsystem, a scene boundary, or a runtime
flow must update this document in the same change. See
[CODING_GUIDELINES.md](../CODING_GUIDELINES.md#documentation-maintenance).

---

## 1. Repository Layout

```text
TD/
|-- Assets/
|   |-- Editor/                 # Editor-only tooling (TD.Editor asmdef)
|   |-- Prefabs/
|   |-- Scenes/                 # Boot, Menu, Gameplay
|   |-- ScriptableObjects/      # Tower/Enemy/Bullet/Level/Menu data assets
|   |-- Scripts/                # Runtime gameplay code (TD.Runtime asmdef)
|   |-- Sprites/
|   `-- Tests/
|       `-- EditMode/           # NUnit EditMode tests (TD.Tests.EditMode asmdef)
|-- Packages/
|-- ProjectSettings/
|-- docs/
|-- tools/
|-- CODING_GUIDELINES.md
|-- README.md
`-- TD.slnx
```

`Assets/Scripts/` is grouped by subsystem; one folder per subsystem, each under a
`TD.<Subsystem>` namespace (`TD.Core`, `TD.Grid`, `TD.Pathfinding`, `TD.Enemies`,
`TD.Towers`, `TD.Bullets`, `TD.Combat`, `TD.Level`, `TD.UI`, `TD.Menu`, `TD.Theming`;
editor code is `TD.Editor`, tests are `TD.Tests.EditMode`). The enemy subsystem uses
the plural `TD.Enemies` to avoid colliding with the `Enemy` type. Three assembly
definitions split runtime, editor, and test code so the test assembly can reference
runtime code without leaking editor-only types.

---

## 2. Subsystems

Runtime code is grouped by domain:

| Subsystem   | Folder                        | Responsibilities |
| ----------- | ----------------------------- | ---------------- |
| Core        | `Assets/Scripts/Core/`        | Session state, game state, build placement, difficulty, pooling |
| Grid        | `Assets/Scripts/Grid/`        | Grid cells, build/path occupancy, cached enemy paths; preview overlay rendering split into `GridPreviewRenderer` |
| Pathfinding | `Assets/Scripts/Pathfinding/` | BFS pathfinding |
| Enemy       | `Assets/Scripts/Enemy/`       | Enemy stats, runtime enemies, round spawning; round composition split into `WavePlanner` |
| Towers      | `Assets/Scripts/Towers/`      | Towers, upgrades, selection, range indicators; targeting via `ITargetProvider` |
| Bullets     | `Assets/Scripts/Bullets/`     | Projectile stats and projectile runtime behaviour |
| Combat      | `Assets/Scripts/Combat/`      | Shared damage contract, rocks, destructible blockers |
| Level       | `Assets/Scripts/Level/`       | Level data, map seeds, loading, camera framing, map occupants |
| UI          | `Assets/Scripts/UI/`          | HUD and runtime map editor |
| Menu        | `Assets/Scripts/Menu/`        | Title screen and main menu |
| Theming     | `Assets/Scripts/Theming/`     | Map theme definitions and theme application |
| Editor      | `Assets/Editor/`              | Editor-only map and export tooling |

---

## 3. Scenes & Runtime Flow

Three scenes drive the game:

| Scene | Purpose |
| ----- | ------- |
| [Boot.unity](../Assets/Scenes/Boot.unity) | Minimal entry point; bootstraps persistent services |
| [Menu.unity](../Assets/Scenes/Menu.unity) | Title screen and main menu, no gameplay state |
| [Gameplay.unity](../Assets/Scenes/Gameplay.unity) | Grid, level, towers, enemies, HUD, runtime map editor |

```text
Boot -> TitleScreenController -> Menu
                              |
                              |-- MainMenuController
                              |   |-- Campaign -> GameSession.SelectLevel()        -> Gameplay
                              |   |-- Custom   -> GameSession.SelectMapSeed()      -> Gameplay
                              |   `-- Editor   -> GameSession.BeginMapEditorMode() -> Gameplay
                              |
                              `-- RuntimeMapEditorController
                                  |-- Paint path / terrain / occupants / start / goal
                                  |-- Save Custom Map
                                  `-- Test Run -> Gameplay round 1 with current seed

Gameplay
  LevelLoader -> GridManager -> Pathfinder
  LevelLoader -> OccupantSpawner / GroundOverlaySpawner / MapThemeApplier
  EnemySpawner -> Enemy.ActiveEnemies
  BuildManager -> Tower -> ITargetProvider -> Enemy.ActiveEnemies / Destructible.MarkedTargets
  Tower -> Bullet via PrefabPool
  TowerSelectionController -> RangeIndicator + InGameHudController
  GameState -> InGameHudController
```

Cross-scene state lives on `GameSession` (static): selected level, map seed,
difficulty, editor flags. Runtime state lives on `GameState` (singleton
MonoBehaviour): current round and money.

---

## 4. Data Model

Gameplay data is authored as ScriptableObjects under `Assets/ScriptableObjects/`:

| Asset type | Defines |
| ---------- | ------- |
| `TowerData` | Tower name, icon, placement cost, damage, attack speed, range, default `BulletData`, tower prefab |
| `TowerUpgradeData` | Per-tower upgrade ladder: upgrade name, cost, damage/speed/range bonuses, optional bullet override |
| `EnemyData` | Enemy name, health, speed, shield, armor, kill reward |
| `BulletData` | Projectile name, travel speed, damage multiplier, splash, piercing, slow, prefab, hit animator |
| `LevelData` | Grid size, map seed, start/goal, path sequences, path cells, ground overrides, occupants (legacy `blockedCells` migrated on load) |
| `MainMenuConfig` | Menu title, main button actions, campaign title, campaign levels and seeds |
| `MapThemeDefinition` | Ground visuals and map theme colours |

Maps support two interchangeable representations:

- **Explicit cells** - `LevelData.startCell`, `goalCell`, `pathSequences`
  (one ordered cell list per enemy route; shared cells form natural
  split / merge junctions for future enemy AI), `pathCells` (union cache),
  `groundOverrides`, `occupants`.
- **Seed string** - JSON encoded via `LevelMapSeedUtility`, decoded into a
  `LevelMapDefinition` and applied with `LevelData.ApplyDefinition()`.

Map definitions use `GroundType` (`Ground`, `Path`, `Elevated`, `Water`, `Lava`)
for terrain and `OccupantType` (`None`, `Rock`, `Destructible`) for objects placed
on top of tiles. Schema version 3 enforces a single source of truth for blocked
tiles: legacy `blockedCells` are migrated into `occupants` of type `Rock` during
`Normalize()` and the field is then cleared. Maps may be at most
`LevelMapDefinition.MaxSize` (70) cells per side.

Custom maps created at runtime are persisted by `CustomMapStorage` as
`Application.persistentDataPath/customMaps.json`, with one-time migration from the
legacy PlayerPrefs entry.

---

## 5. Public API Quick Reference

This is the contract the rest of the codebase relies on. Update this section whenever a
public method or property is added, removed, or renamed.

### Core

- `GameSession` (static) - `SelectLevel`, `SelectMapSeed`, `SelectMapDefinition`,
  `ClearSelectedLevel`, `BeginTestRun`, `EndTestRun`, `SelectDifficulty`,
  `BeginMapEditorMode`, `EndMapEditorMode`; properties `SelectedLevelData`,
  `SelectedMapDefinition`, `SelectedMapSeed`, `IsEditorTestRun`,
  `SelectedDifficulty`, `IsMapEditorSession`.
- `GameState` - `Instance`, `CurrentRound`, `Money`; events `OnRoundChanged`,
  `OnMoneyChanged`; methods `GetOrCreate`, `ResetState`, `SetRound`, `AddMoney`,
  `TrySpendMoney`, `ResetMoney`.
- `DifficultySettings` - fields `level`, `healthMultiplier`, `speedMultiplier`,
  `rewardMultiplier`, `amountMultiplier`, `amountScaleMultiplier`.
- `BuildManager` - `IsPlacingTower`, `SelectedTowerToBuild`, `AvailableTowers`;
  event `OnBuildSelectionChanged`; methods `SelectTowerToBuild`,
  `ClearSelectedTowerToBuild`, `CanAfford`, `SellTower`, `ClearAllPlacedTowers`.
- `Difficulty` - enum `DifficultyLevel { Easy, Normal, Hard, Nightmare }`,
  `DifficultySettings.ForLevel(level)`.
- `PrefabPool` (static) - `Spawn(prefab, position, rotation)`, `Release(instance)`,
  `Clear()`.
- `PooledObject` - `SourcePrefab` marker used by `PrefabPool`.

### Grid & Pathfinding

- `GridManager` - `StartCell`, `GoalCell`, `CellSize`, `HasGrid`,
  `UsesExplicitPath`, `Width`, `Height`; event `OnPathChanged`; methods
  `BuildGrid`, `BuildGridPreview`, `ClearPreviewVisuals`, `GetCell`,
  `GetNeighbors`, `WorldToCell`, `CellToWorld`, `IsReservedPathCell`,
  `IsPathCell`, `CanEnemyWalkOn`, `CanBuildAt`, `GetGroundType`,
  `GetCachedEnemyPathWorld`, `TryOccupyCell`, `ClearOccupiedCell`,
  `ClearBlockedCell`, `WouldOccupyingCellBlockPath`.
- `GridPreviewRenderer` - owns the map-editor preview overlay; `Build`,
  `Clear`, `DisposeSprite`. Used internally by `GridManager`.
- `GridCell` - `X`, `Y`, `Position`, `IsBlocked`, `IsOccupied`, `IsPath`,
  `IsWalkable`, `SetBlocked`, `SetPath`, `SetOccupied`.
- `Pathfinder` - `FindPath(start, goal)`, `HasPath(start, goal)`.

### Enemy & Combat

- `Enemy` - `ActiveEnemies`, `IsDead`, `Data`, `Reward`, `CurrentHealth`,
  `MaxHealth`, `WorldPosition`; events `OnDied`, `OnReachedGoal`; methods
  `Initialize`, `SetWaypoints`, `TakeDamage`, `ApplySlow`.
- `EnemyData` - public fields `enemyName`, `maxHealth`, `speed`, `shield`,
  `armor`, `reward`.
- `EnemySpawnEntry` - enemy data/prefab plus round scaling fields
  `firstRound`, `baseAmount`, `amountPerRound`, `spawnInterval`.
- `EnemySpawner` - `BeginSpawning`, `RestartSpawning`, `RestartSpawningFromRound`,
  `StopSpawning(clearEnemies)`.
- `WavePlanner` (static) - `BuildRound(output, spawnEntries, round, difficulty,
  fallback)`, `IsValid(entry)`. Pure round-composition logic extracted from
  `EnemySpawner` for testability.
- `IDamageable` - `CurrentHealth`, `MaxHealth`, `IsDead`, `WorldPosition`,
  `TakeDamage`.
- `Destructible` - `MarkedTargets`, `ActiveTargets`, `IsMarked`,
  `ClearMarkedTargets`, `Initialize`, `TakeDamage`, `ToggleMarked`, `Mark`,
  `Unmark`.
- `ITargetProvider` - `FindTarget(origin, range)`; abstraction that decouples
  towers from the global enemy/destructible registries.
- `Rock` - marker component for indestructible occupant objects.

### Towers & Bullets

- `Tower` - `Data`, `CurrentUpgradeLevel`, `Damage`, `AttackSpeed`, `Range`,
  `Initialize`, `SetTerrainRangeBonus`, `SetTargetProvider`, `CanUpgrade`,
  `GetNextUpgradeCost`, `TryUpgrade`, `GetSellValue`.
- `DefaultTargetProvider` - `ITargetProvider` implementation (singleton
  `Instance`); marked destructibles first, then nearest enemy in range.
- `TowerData` - public fields `towerName`, `icon`, `cost`, `damage`,
  `attackSpeed`, `range`, `bulletData`, `towerPrefab`.
- `TowerUpgradeData` - nested `UpgradeLevel` with `upgradeName`, `cost`,
  `damageBonus`, `attackSpeedBonus`, `rangeBonus`, `overrideBulletData`; field
  `levels`.
- `BulletData` - public fields `bulletName`, `travelSpeed`, `damageMultiplier`,
  `splashRadius`, `isPiercing`, `slowFactor`, `slowDuration`, `bulletPrefab`,
  `hitAnimator`.
- `RangeIndicator` - `Show(center, radius, color)`, `Hide()`.
- `TowerSelectionController` - `Configure`, `DeselectTower`, `RefreshTowerRange`.
- `Bullet` - `Initialize(bulletData, damage, target)`.

### Level

- `LevelData` - `width`, `height`, `mapSeed`, `startCell`, `goalCell`,
  `blockedCells` (legacy, cleared after `Normalize`), `pathCells`,
  `pathSequences`, `groundOverrides`, `occupants`;
  `GetMapDefinition`, `TryGetMapDefinition`, `ApplyDefinition`.
- `LevelMapDefinition` - constants `CurrentVersion` (3), `MaxSize` (70); fields
  `version`, `width`, `height`, `startCell`, `goalCell`, `blockedCells` (legacy),
  `pathCells`, `pathSequences`, `groundOverrides`, `occupants`; properties
  `HasExplicitPath`, `HasMultiplePaths`; methods `FromLegacy`, `Clone`,
  `CloneNormalized`, `Normalize`, `IsPath`, `GetGround`, `TryGetOccupant`,
  `IsBuildable`, `IsInBounds`.
- `PathSequence` - serializable ordered cell list (`cells`) describing one enemy route.
- `GroundType` - enum `Ground`, `Path`, `Elevated`, `Water`, `Lava`.
- `OccupantType` - enum `None`, `Rock`, `Destructible`.
- `GroundOverrideEntry` - fields `cell`, `type`.
- `OccupantEntry` - fields `cell`, `type`, `maxHp`, `reward`.
- `LevelMapSeedUtility` - `SeedPrefix`, `Encode`, `ToJson`, `TryDecode`.
- `LevelMapValidator` - `Validate`.
- `CustomMapEntry` - fields `label`, `seed`.
- `CustomMapCollection` - field `maps`.
- `CustomMapStorage` - `GetAll`, `Save`.
- `MapGenerator` - `ScatterParams`, `GeneratePath`, `ScatterBlocks`,
  `GenerateFullMap`, `GenerateForkAndMerge`.
- `LevelLoader` - `DefaultLevelData`, `HasLoadedLevel`, `LoadedMapDefinition`;
  events `OnLevelLoaded`, `OnMapLoaded`; methods `LoadSelectedOrDefaultLevel`,
  `LoadLevel`, `LoadMapSeed`, `LoadMap`.
- `GroundOverlaySpawner` - renders `groundOverrides` on level/map load.
- `OccupantSpawner` - spawns rocks/destructibles from `occupants` and routes
  click-to-mark targeting.
- `MapCameraFrame` - `IsValid`, `CameraRect`, `OrthographicSize`, `Aspect`,
  `Center`, `MinCenter`, `MaxCenter`, `CanPan`, `ClampPosition`.
- `MapCameraController` - `Configure(camera, frame)`, `Configure(frame)`.
- `MapCameraFramer` - `Frame(targetCamera, definition, cellSize,
  reservedRightUiWidth, targetCanvas, paddingCells, overzoomFactor)`.

### UI & Menu

- `InGameHudController` - `PanelWidth`; event `OnBackToEditorRequested`; methods
  `Show`, `Hide`, `ShowTower`, `ClearContext`.
- `HealthBar` - `Bind`, `Unbind`, `AttachTo`.
- `RuntimeMapEditorController` - `Open`, `CloseToMenu`.
- `MainMenuController` - `ShowMainMenu`, `HideMenu`.
- `TitleScreenController` - drives splash and transition to Menu.
- `MainMenuConfig` - `title`, `mainButtons`, `campaignTitle`, `campaignLevels`.
- `MainMenuAction` - enum `SingleCampaign`, `Infinite`, `Challenge`,
  `TowerUpgrade`, `Options`, `MapEditor`, `CustomMaps`, `Exit`.
- `MainMenuButtonConfig` - fields `label`, `action`, `isEnabled`; constructors.
- `CampaignLevelConfig` - fields `label`, `levelData`, `mapSeed`, `isUnlocked`;
  constructors.

### Theming

- `MapThemeDefinition` - fields `themeId`, `displayName`, `backgroundSprite`,
  `backgroundColor`, `groundVisuals`; `TryGetGroundVisual`.
- `MapThemeDefinition.GroundVisual` - fields `type`, `sprite`, `tint`.
- `MapThemeApplier` - static `Active`; property `ActiveTheme`; method
  `SetTheme`.

### Editor

- `LevelMapEditorWindow` - menu `Tools/Tower Defense/Map Editor`, `OpenWindow()`.
- `AssetsStructureExporter` - menu `Tools/Export/Export Assets Folder To TXT`,
  `ExportAssetsFolderToTxt()`.

---

## 6. Conventions In Effect

- Code, identifiers, commit messages, PR descriptions, comments, and runtime UI text
  should be English.
- See [CODING_GUIDELINES.md](../CODING_GUIDELINES.md) for style and lifecycle rules.
- Avoid allocations in `Update()` in hot gameplay paths.
- Bullets and enemies should spawn/despawn via `PrefabPool`.
- `mapSeed` is the portable representation for maps. Explicit `pathSequences`
  (and the derived `pathCells` union cache) are used when a map was authored with
  one or more fixed enemy routes.

---

## 7. Known Gaps & Suggested Next Steps

- **HUD/Menu UI source** - menus and HUD are constructed from code at runtime.
  Once the layout stabilizes, migrating to UXML or prefabs would make iteration and
  theming easier.
- **Test coverage expansion** - `EnemySpawner` round scaling, `BuildManager`
  placement rules, and runtime map editor flows need PlayMode coverage.
- **Visual polish** - several runtime visuals are generated from tinted 1x1 sprites
  and should eventually move to authored assets.

---

## 8. Change Log

Append a one-line entry whenever this document is updated.

- 2026-06-03: Initial architecture snapshot extracted from code review of 30 C# files.
- 2026-06-03: Introduced asmdef structure (`TD.Runtime`, `TD.Editor`,
  `TD.Tests.EditMode`); added EditMode test suite under `Assets/Tests/EditMode/`.
  `CustomMapStorage` now persists to `Application.persistentDataPath/customMaps.json`
  and auto-migrates from the legacy PlayerPrefs key.
- 2026-06-04: Repaired markdown encoding artifacts, refreshed subsystem/API notes,
  and documented English-only runtime UI/comment convention.
- 2026-06-04: Completed public API coverage for combat, level generation,
  runtime helpers, theming, UI helpers, menu config types, and ScriptableObject
  schemas.
- 2026-06-04: Introduced `TD.<Subsystem>` namespaces across runtime/editor/tests
  (enemy domain uses `TD.Enemies`). Decoupled tower targeting behind
  `ITargetProvider`/`DefaultTargetProvider`, split `GridManager` preview rendering
  into `GridPreviewRenderer`, and extracted `EnemySpawner` round composition into
  the testable `WavePlanner`.
