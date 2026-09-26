# Tower Defense - Architecture Overview

Last reviewed: September 2026.

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
|       |-- EditMode/           # NUnit EditMode tests (TD.Tests.EditMode asmdef)
|       `-- PlayMode/           # Gameplay integration tests (TD.Tests.PlayMode asmdef)
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
editor code is `TD.Editor`, tests are `TD.Tests.EditMode` and `TD.Tests.PlayMode`).
The enemy subsystem uses the plural `TD.Enemies` to avoid colliding with the `Enemy`
type. Four assembly definitions split runtime, editor, EditMode tests, and PlayMode
tests so test code can reference runtime code without leaking editor-only types.

---

## 2. Subsystems

Runtime code is grouped by domain:

| Subsystem   | Folder                        | Responsibilities |
| ----------- | ----------------------------- | ---------------- |
| Core        | `Assets/Scripts/Core/`        | Session state, match lifecycle/cleanup, build placement, difficulty, pooling |
| Grid        | `Assets/Scripts/Grid/`        | Grid cells, build/path occupancy, cached enemy paths; preview overlay rendering split into `GridPreviewRenderer` |
| Pathfinding | `Assets/Scripts/Pathfinding/` | BFS over the read-only `IPathGrid` contract |
| Enemy       | `Assets/Scripts/Enemy/`       | Enemy stats, runtime enemies, round spawning; round composition split into `WavePlanner` |
| Towers      | `Assets/Scripts/Towers/`      | Towers, upgrades, selection, range indicators; per-instance targeting modes via `ITargetProvider` |
| Bullets     | `Assets/Scripts/Bullets/`     | Projectile stats and straight predictive projectile flight |
| Combat      | `Assets/Scripts/Combat/`      | Shared damage contract, physics synchronization, rocks, destructible blockers |
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

In regular campaign and custom-map sessions, `EnemySpawner` starts automatically
and waits until `GridManager` has built a valid grid. Map-editor sessions suppress
that automatic start; `RuntimeMapEditorController` starts spawning explicitly only
when the user begins a test run.

Cross-scene state lives on `GameSession` (static): selected level, map seed,
difficulty, editor flags. Runtime state lives on `GameState` (singleton
MonoBehaviour): current round, money, base lives, configured maximum rounds, and
the authoritative `Playing` / `Won` / `Lost` match state. MVP restarts reload the
`Gameplay` scene; `GameState.ResetState()` restores the complete starting state.
`GameState` is scene-local and is destroyed on scene unload.
Selecting a campaign level or custom map clears editor/test flags. Entering the map
editor clears gameplay selection, so a previous session cannot leak into the next
mode even when navigation did not follow the normal menu cleanup path.

`InGameHudController` observes the match state. It displays lives and finite wave
progress, blocks build/selection actions after the match, and presents the final
result. Restart reloads `Gameplay`; returning to the menu clears transient session
selection and editor/test flags. Editor test runs replace the menu action with a
return to the runtime editor.

`GameplayLifecycle.CanRunCombat` gates enemy movement, tower attacks, projectiles,
and destructible interaction in editor authoring and after match end. On match
end, `GameState` calls `GameplayLifecycle.StopCombat` to release active bullets and
clear marked targets before notifying subscribers; `EnemySpawner` clears enemies
and its spawn queue. The result overlay leaves the level and towers visible.

Editor return goes through `GameplayLifecycle.ReturnToEditor`: end test mode, stop
spawning, release projectiles, clear targets/towers/placement ghosts, and call
`LevelLoader.ClearLoadedLevel`. Its `OnLevelCleared` event clears occupants, ground
tiles and theme background. Objects are deactivated before deferred destruction.
The HUD clears selection and range indicators. Both campaign and editor test runs
render gameplay through `GroundOverlaySpawner`; only authoring uses preview tiles.

Combat update order is enemy movement (-100), normal spawning (0), towers (100),
then bullets (200). `CombatPhysics.Synchronize` reuses the frame's physics snapshot
unless a damageable spawn, movement or disable invalidates it. External systems
moving damageables during combat must call `Invalidate`. EditMode queries always
synchronize, since the frame counter does not provide a simulation boundary there.

---

## 4. Data Model

Gameplay data is authored as ScriptableObjects under `Assets/ScriptableObjects/`:

| Asset type | Defines |
| ---------- | ------- |
| `TowerData` | Tower name, icon, placement cost, damage, attack speed, range, default `BulletData`, tower-specific upgrade path, tower prefab |
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

Map data, seed encoding, validation and persistence live in separate source files:
`LevelMapDefinition`, `LevelMapSeedUtility`, `LevelMapValidator`, and `CustomMapStorage`.
Custom maps are persisted to `Application.persistentDataPath/customMaps.json`.
The internal `CustomMapStore` accepts a file path and legacy-data callbacks for
isolated filesystem tests. Saves write a temporary file in the same directory and
replace the existing file atomically (or rename on first save). Write failures are
returned to the UI; no PlayerPrefs fallback can silently supersede a disk save.
Unreadable/damaged files prevent writes and remain untouched. Legacy PlayerPrefs
data is imported only when the file is absent, and deleted only after a successful
write. `GetAll` logs load errors; restore a known-good file before saving again.

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
- `MatchState` - enum `Playing`, `Won`, `Lost`.
- `GameState` - `Instance`, `CurrentRound`, `Money`, `Lives`, `StartingLives`,
  `MaxRounds`, `State`, `IsPlaying`; events `OnRoundChanged`, `OnMoneyChanged`,
  `OnLivesChanged`, `OnMatchEnded`; methods `GetOrCreate`, `ResetState`,
  `SetRound`, `AddMoney`, `TrySpendMoney`, `DamageBase`, `Win`, `Lose`.
- `DifficultySettings` - fields `level`, `healthMultiplier`, `speedMultiplier`,
  `rewardMultiplier`, `amountMultiplier`, `amountScaleMultiplier`.
- `BuildManager` - `IsPlacingTower`, `SelectedTowerToBuild`, `AvailableTowers`;
  event `OnBuildSelectionChanged`; methods `SelectTowerToBuild`,
  `ClearSelectedTowerToBuild`, `CanAfford`, `TryBuildAtCell`, `SellTower`, `ClearAllPlacedTowers`.
  Placement validates the cell and match, charges money, refunds failed placement,
  and initializes the tower with its own upgrade path or the serialized fallback.
- `GameplayLifecycle` (static) - `CanRunCombat`, `StopCombat`,
  `ReturnToEditor(spawner, builder, loader)` coordinate combat and editor cleanup.
- `Difficulty` - enum `DifficultyLevel { Easy, Normal, Hard, Nightmare }`,
  `DifficultySettings.ForLevel(level)`.
- `PrefabPool` (static) - `Spawn(prefab, position, rotation)`, `Release(instance)`,
  `Clear()`. Releasing an already pooled instance has no effect, including when
  match-end cleanup overlaps an enemy's own release.
- `PooledObject` - `SourcePrefab` marker used by `PrefabPool`.

### Grid & Pathfinding

- `GridManager` - `StartCell`, `GoalCell`, `CellSize`, `HasGrid`,
  `UsesExplicitPath`, `Width`, `Height`; event `OnPathChanged`; methods
  `BuildGrid`, `BuildGridPreview`, `ClearPreviewVisuals`, `GetCell`,
  `GetNeighbors`, `WorldToCell`, `CellToWorld`, `IsReservedPathCell`,
  `IsPathCell`, `CanEnemyWalkOn`, `CanBuildAt`, `GetGroundType`,
  `GetCachedEnemyPathWorld`, `TryOccupyCell`, `ClearOccupiedCell`,
  `ClearBlockedCell`, `WouldOccupyingCellBlockPath`, `UpdatePreviewCell`.
- `GridPreviewRenderer` - owns the map-editor preview overlay; `Build`,
  `Clear`, `DisposeSprite`, `UpdateCell`. Used internally by `GridManager`.
- `GridCell` - `X`, `Y`, `Position`, `IsBlocked`, `IsOccupied`, `IsPath`,
  `IsWalkable`, `SetBlocked`, `SetPath`, `SetOccupied`.
- `IPathGrid` - `GetCell(position)`, `CanEnemyWalkOn(cell)`; implemented by
  `GridManager` or a pure-data grid. `Pathfinder` has no MonoBehaviour dependency.
- `Pathfinder` - constructor accepts `IPathGrid`; `FindPath(start, goal)`,
  `HasPath(start, goal)`. Enumerates four directions without per-cell neighbor lists.

### Enemy & Combat

- `Enemy` - `ActiveEnemies`, `IsDead`, `Data`, `Reward`, `GoalDamage`,
  `CurrentHealth`, `MaxHealth`, `CurrentSpeed`, `PathProgress`, `WorldPosition`;
  events `OnDied`, `OnReachedGoal`; methods `Initialize`, `SetWaypoints`,
  `PredictPosition`, `TakeDamage`, `ApplySlow`.
- `EnemyPath` - immutable waypoint snapshot shared by all enemies on a route;
  `Count`, indexer, `Advance(position, ref waypointIndex, distance)`, `GetProgress`.
  Movement and prediction use the same distance-budget traversal. Cached suffix
  lengths make path-progress queries O(1). List-based enemy initialization/path
  replacement overloads remain available and create a snapshot.
- `EnemyData` - public fields `enemyName`, `maxHealth`, `speed`, `shield`,
  `armor`, `goalDamage`, `reward`.
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
- `ITargetProvider` - `FindTarget(origin, range, targetingMode)`; abstraction that decouples
  towers from the global enemy/destructible registries.
- `Rock` - marker component for indestructible occupant objects.
- `CombatPhysics` (static) - `Synchronize`, `Invalidate`; see combat update order above.

### Towers & Bullets

- `Tower` - `Data`, `CurrentUpgradeLevel`, `Damage`, `AttackSpeed`, `Range`,
  `TargetingMode`, `Initialize`, `SetTerrainRangeBonus`, `SetTargetProvider`,
  `SetTargetingMode`, `CycleTargetingMode`, `CanUpgrade`, `GetNextUpgradeCost`,
  `TryUpgrade`, `GetSellValue`. Tower prefabs keep the base visual and gameplay
  components on a fixed root. A child `TurretPivot` tracks the current target,
  contains the rotating `Head`, and owns the `FirePoint` used to spawn shots.
  Towers retain a valid target between attacks and fire once the pivot is within
  its configured angular tolerance. Projectile turrets and projectile flight use
  the same predicted intercept point, while instant laser shots track the target's
  current position.
- `TowerUpgradeService` - `TryPurchase(tower, gameState)` owns match/affordability
  checks, payment and upgrade application/refund. The HUD delegates the purchase.
  `Tower.TryUpgrade` remains the low-level stat mutation, without payment.
- `DefaultTargetProvider` - `ITargetProvider` implementation (singleton
  `Instance`); marked destructibles first, then an enemy selected per tower by
  path progress, current health, or effective speed.
  Each candidate's metric is evaluated once per search. Towers without a target
  retry at 0.1-second intervals instead of every frame; firing cadence is unchanged.
- `TowerData` - public fields `towerName`, `icon`, `cost`, `damage`,
  `attackSpeed`, `range`, `bulletData`, `upgradeData`, `towerPrefab`; derived combat values
  `DamagePerShot`, `AttacksPerSecond`, `TargetingRange`, and `GetTargetingRange`.
- `TowerUpgradeData` - nested `UpgradeLevel` with `upgradeName`, `cost`,
  `damageBonus`, `attackSpeedBonus`, `rangeBonus`, `overrideBulletData`; field
  `levels`.
- `BulletData` - `BulletType` selects a travelling projectile or instant laser;
  fields configure travel speed, laser length/width/duration, damage, attack
  speed and range multipliers, splash, piercing, slow, prefab, and hit animator.
  `ModifyDamage`, `ModifyAttackSpeed`, and `ModifyRange` apply the multipliers.
- `RangeIndicator` - `Show(center, radius, color)`, `Hide()`.
- `TowerSelectionController` - `Configure`, `DeselectTower`, `RefreshTowerRange`.
- `Bullet` - `Destination`, `Initialize(bulletData, damage, target)`, `ReleaseAll`; predicts an
  enemy position using its current effective speed at launch, then keeps a fixed
  straight trajectory. Projectiles sweep their collider along each movement step
  and damage only an enemy or marked destructible they actually intersect. A
  missed direct shot deals no damage; splash resolves at its impact or endpoint.
  Lasers damage only targets intersected by the configured beam.
  Queries reuse per-instance result lists and a target set; splash and piercing
  lasers damage each target once even with multiple colliders. The serialized
  `hitLayers` mask defaults to all layers for existing prefab compatibility.

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
- `LevelMapSeedUtility` - `SeedPrefix`, `Encode`, `ToJson`, `TryDecode`, `TryDecodeRaw`.
- `LevelMapAuthoringState` - `MapDefinition`, `HasExplicitPath`, `CreateNewMap`,
  `LoadDefinition`, `BuildDefinition`, `PaintCell`, `GenerateRandomPath`,
  `ScatterRandomBlocks`, `TryGetOccupant`, `TryGetGroundOverride`, `IsPathCell`.
- `LevelMapValidator` - `Validate`.
- `CustomMapEntry` - fields `label`, `seed`.
- `CustomMapCollection` - field `maps`.
- `CustomMapStorage` - `GetAll`, `Save`.
- `MapGenerator` - `ScatterParams`, `GeneratePath`, `ScatterBlocks`,
  `GenerateFullMap`, `GenerateForkAndMerge`.
- `LevelLoader` - `DefaultLevelData`, `HasLoadedLevel`, `LoadedMapDefinition`;
  events `OnLevelLoaded`, `OnMapLoaded`, `OnLevelCleared`; methods `LoadSelectedOrDefaultLevel`,
  `LoadLevel`, `LoadMapSeed`, `LoadMap`, `ClearLoadedLevel`.
- `GroundOverlaySpawner` - renders the complete gameplay grid on level/map load,
  including buildable ground, path, start/goal cells, and special terrain. Runtime
  map-editor authoring continues to use `GridPreviewRenderer` instead.
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
  Painting updates only changed cells (including previous start/goal cells).
  Seed encoding and validation run after 0.15 seconds without another edit or
  on mouse release; save/test buttons are disabled while validation is pending.
  Loading/resizing/generating a map still performs a full preview rebuild.
- `MainMenuController` - `ShowMainMenu`, `HideMenu`.
- `TitleScreenController` - drives splash and transition to Menu.
- `MainMenuConfig` - `title`, `mainButtons`, `campaignTitle`, `campaignLevels`.
- `MainMenuAction` - enum `SingleCampaign`, `Infinite`, `Challenge`,
  `TowerUpgrade`, `Options`, `MapEditor`, `CustomMaps`, `Exit`.
- `MainMenuButtonConfig` - fields `label`, `action`, `isEnabled`; constructors.
- `CampaignLevelConfig` - fields `label`, `levelData`, `mapSeed`, `isUnlocked`;
  constructors.

#### Desktop display and focus policy

- On first launch, the Windows player defaults to a resizable 1920 x 1080
  window. The options UI reads the active `Screen` state and applies changes
  with `Screen.SetResolution`.
- Unity is the only owner of display persistence. [Unity 6 stores resolution,
  full-screen mode, and window position after `Screen` changes and restores
  them before user code runs](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Screen.html).
  Do not mirror them in project-specific `PlayerPrefs` or reapply them from a
  runtime initialization callback.
- `runInBackground` is intentionally disabled. This is a real-time
  single-player game without a separate focus-loss pause flow, so waves and
  enemies must not continue while the player is using another application.
  Revisit this only together with an explicit pause/lifecycle design.

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
- **Release validation** - automated tests cover campaign build/upgrade/sale/reload,
  repeated editor tests, projectiles and match state. Manual input/layout checks,
  a complete balanced playthrough, standalone-player validation and profiler
  measurements at the intended enemy/tower/map scale remain required.
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
- 2026-09-16: Documented campaign auto-spawning and the map-editor guard used to
  keep enemy spawning exclusive to explicit editor test runs.
- 2026-09-16: Expanded `GroundOverlaySpawner` to render the complete gameplay
  grid while leaving map-editor authoring visuals with `GridPreviewRenderer`.
- 2026-09-16: Added the authoritative `GameState` match lifecycle with base
  lives, maximum rounds, guarded economy, and idempotent win/loss transitions.
- 2026-09-16: Connected `EnemySpawner` to the match lifecycle, added per-enemy
  goal damage, stopped spawning on loss, and completed the match after the final
  configured wave is empty.
- 2026-09-16: Added lives and finite-wave HUD output, match-end overlays,
  scene-reload restart, menu cleanup, and post-match build/selection locks.
- 2026-09-16: Added straight predictive projectile flight and independent per-tower
  targeting modes for path progress, health, and effective enemy speed.
- 2026-09-19: Documented bullet-based tower modifiers and laser configuration;
  made pool releases idempotent and tied straight projectile and laser damage to
  physical path intersections, with slowed speed used for shot leading.
- 2026-09-21: Added atomic custom-map persistence with error reporting; split map
  model/codec/validator/storage; introduced shared EnemyPath traversal, cached
  progress, IPathGrid, batched physics synchronization, reusable query results,
  deduplicated splash, incremental previews, guarded GameSession transitions,
  GameplayLifecycle cleanup and UI-independent tower transactions.
- 2026-09-21: Split tower visuals into a fixed base plus a target-tracking turret
  pivot and moved projectile spawning to per-prefab fire points.
- 2026-09-21: Added per-tower upgrade paths, reduced laser upgrade range growth,
  adjusted laser cadence, and introduced a lower-DPS Long Range Tower using normal bullets.
- 2026-09-21: Unified turret aiming and projectile lead prediction so moving-target
  shots leave the barrel along the direction the turret actually tracks.
