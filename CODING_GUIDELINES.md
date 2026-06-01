Coding & Git Conventions

Purpose
- Keep code clean, readable and maintainable.
- Code must be written in English only (identifiers, comments, commit messages, PR descriptions).
- Prefer self-explanatory code; minimize comments.

Principles
- Self-documenting code: use clear, descriptive names instead of comments.
- Small, focused commits and pull requests.
- Consistent formatting and naming across the repository.

Naming rules (high level)
- Use descriptive, context-aware names. Prefer `playerHealth` or `mainMenuController` over `ph` or `mmc`.
- Avoid non-descriptive abbreviations and single-letter identifiers (no `i`, `j`, `cnt` unless the context is trivial and local).
- Use nouns for classes and data structures; use verbs for methods/operations.
- Prefer `rowIndex`/`columnIndex` instead of `i`/`j` in loops where index meaning matters.

C# style (recommended)
- Types, classes, structs, enums: PascalCase (`PlayerController`).
- Public properties and methods: PascalCase (`LoadLevel()`).
- Local variables and method parameters: camelCase (`playerScore`).
- Private fields: camelCase by default (`currentHealth`). Existing underscore-prefixed fields are allowed; keep the style consistent within each file and do not rename solely for style churn.
- Constants: PascalCase or UPPER_CASE (follow existing project convention).

Comments
- Primary rule: avoid comments that repeat what the code does. Prefer refactoring.
- Comments are allowed only to explain *why* something non-obvious exists, or to reference external constraints (APIs, engine bugs, platform quirks).
- Prefer documented design notes in `docs/` over many inline comments.

Git conventions
- Language: English for commit messages and PR descriptions.
- Commit message format: short imperative summary (<=50 chars), blank line, optional body. Prefer Conventional Commits: `type(scope): subject`.
  - Types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `chore`.
  - Example: `feat(menu): add main menu controller`
- Branch names: `feature/<short-description>`, `bugfix/<short-description>`, `hotfix/<short-description>`, `chore/<short-description>`.
- Keep commits small and focused; prefer many well-scoped commits over one large change.

Files & repo hygiene
- Keep Unity-generated folders out of source control (Library/, Temp/, obj/, Build/). Use a proper `.gitignore` for Unity.
- Do not commit large binary assets unnecessarily; prefer LFS for large files.

Enforcement & tools (suggested)
- Add an `.editorconfig` to standardize formatting and some naming rules for Roslyn analyzers.
- Consider adding `StyleCop.Analyzers` (NuGet) and a ruleset to surface naming/format issues.
- Use `dotnet format` or Unity formatting tools before committing.
- Code reviews remain the primary guardrail for the "no comments" rule — automated tools can help but will not fully enforce intent.

Examples
- Good commit: `feat(menu): add main menu controller`
- Bad variable name: `int cnt;` — better: `int enemyCount;`
- Bad abbreviation: `custAddr` — better: `customerAddress`

Exceptions
- Short, explicit comments allowed when a behavior is surprising or required by an external system. Keep them minimal and state the reason.

Unity-specific conventions
- MonoBehaviour lifecycle:
  - Use `Awake()` for self-initialization (e.g. `GetComponent`).
  - Use `Start()` for cross-component dependent setup.
  - Use `OnDestroy()` to release Unity Objects you allocated (e.g. `Texture2D`, `Sprite`, `Material`).
  - Avoid `FindObjectsByType()` / `FindAnyObjectByType()` in `Update()`; cache references in `Awake()`/`Start()`.
- SerializeField / Inspector exposure:
  - Prefer `[SerializeField] private T field;` over `public` fields.
  - Expose read-only access via properties: `public T Field => field;`.
  - Avoid `public` mutable fields on MonoBehaviours.
  - Field prefixes are not mandatory; match the surrounding file style.
- Scene & Prefab naming:
  - Scenes: PascalCase (`Boot`, `Menu`, `Gameplay`).
  - Prefabs: PascalCase, optionally suffixed by role (`BasicEnemy`, `ConfirmDialog`).
  - GameObjects in scene: Name should match the dominant component (`GridManager`, `LevelLoader`) — avoid misleading names like `MainMenu` for an editor controller.
- Asset / folder layout:
  - `Assets/Scripts/<Domain>/` (e.g. `Core`, `Grid`, `UI`, `Towers`, `Enemy`, `Level`, `Menu`, `Pathfinding`).
  - `Assets/Scenes/`, `Assets/Prefabs/`, `Assets/Sprites/`, `Assets/ScriptableObjects/`.
- Scene boundaries:
  - `Boot.unity`: minimal entry point, bootstraps persistent services.
  - `Menu.unity`: `MainMenuController`, `TitleScreenController`, no gameplay state.
  - `Gameplay.unity`: `GridManager`, `LevelLoader`, `BuildManager`, `EnemySpawner`, `InGameHudController`, `RuntimeMapEditorController`.
  - Cross-scene data: prefer ScriptableObject or a dedicated persistent singleton (e.g. `GameSession` with `DontDestroyOnLoad`) over `PlayerPrefs` for transient state.
- Null-safety & validation:
  - Validate serialized references in `Awake()`/`Start()`; on failure, `Debug.LogError` with component name and return early.
  - Use null-conditional and null-coalescing operators where they improve clarity.
- Performance:
  - Use object pooling for frequently spawned/destroyed objects (bullets, enemies, preview cells).
  - Avoid allocations in `Update()` (no `new List<>`, no LINQ, no string concatenation in hot paths).
  - Cache `transform`, components, and lookup results.
- Platform handling:
  - Guard editor-only code with `#if UNITY_EDITOR`.
  - Disable `Application.Quit()` in WebGL builds (use `#if !UNITY_WEBGL`).
- Logging:
  - Prefix logs with the component name: `Debug.LogError("GridManager: ...")`.
  - Keep all log messages in English (per project language rule).

Visual / preview separation
- Runtime gameplay must not spawn debug/preview visuals. Visual previews (e.g. `GridManager.BuildGridPreview`) are reserved for editor/runtime-editor flows.
- Allocated `Texture2D`/`Sprite`/`Material` instances must be destroyed in `OnDestroy()` or when replaced.

Next steps
- If you want, I can add this file to the repository, create a `.editorconfig`, and add a minimal StyleCop/format setup. Tell me which of these I should apply automatically.
