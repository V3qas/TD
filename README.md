# Tower Defense (Unity)

A 2D grid-based tower defense game with a runtime map editor and a campaign mode.

## Documentation

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) - high-level architecture, subsystems, public API contract, and runtime flow.
- [CODING_GUIDELINES.md](CODING_GUIDELINES.md) - coding, naming, commit, and documentation-maintenance rules.

> Any change that affects a public API, a subsystem boundary, a scene, a ScriptableObject schema, or a runtime flow must update `docs/ARCHITECTURE.md` in the same commit.

## Project Layout

```text
Assets/
|-- Editor/                 Editor-only tooling (TD.Editor asmdef)
|-- Prefabs/
|-- Scenes/                 Boot, Menu, Gameplay
|-- ScriptableObjects/      Tower/Enemy/Bullet/Level/Menu data assets
|-- Scripts/                Runtime gameplay code (TD.Runtime asmdef)
|-- Sprites/
`-- Tests/
    `-- EditMode/           NUnit EditMode tests (TD.Tests.EditMode asmdef)
docs/
tools/
```

## Running The Game

1. Open the project in Unity.
2. Open `Assets/Scenes/Boot.unity` and press Play.
3. Boot -> Menu -> Gameplay (campaign level or runtime map editor).

## Building From The Command Line

```powershell
dotnet build TD.slnx
```

This compiles all generated `.csproj` files. Unity warnings from packages are expected.

## Tests

EditMode unit tests cover the pure-data layers: `Pathfinder`, `LevelMapDefinition`,
`LevelMapValidator`, `LevelMapSeedUtility`, `Difficulty`, and `MapCameraFrame`.

Run them from Unity via **Window -> General -> Test Runner -> EditMode -> Run All**.

## Tools

- **Tools -> Tower Defense -> Map Editor** - author levels in the editor (`Assets/Editor/LevelMapEditorWindow.cs`).
- **Main Menu -> Map Editor** - paint a map at runtime, save as Custom Map, run a test round (`Assets/Scripts/UI/RuntimeMapEditorController.cs`).
- **Tools -> Export -> Export Assets Folder To TXT** - dump source/asset summaries to `AssetsExport/`.

## Persistence

- **Custom maps** - JSON file at `Application.persistentDataPath/customMaps.json`, with one-time migration from the legacy `PlayerPrefs` entry.
- **Cross-scene state** - `GameSession` (static) and `GameState` (singleton MonoBehaviour).
