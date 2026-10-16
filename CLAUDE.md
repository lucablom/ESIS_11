# Notes for Claude

- Unity 6, 2D. The game text is English; talk to Luca (repo owner) in Swedish.
- Claude cannot run the Unity editor. Write scripts and editor tools; Luca wires up scenes, prefabs and UI and tests in Play mode.
- Keep game logic in plain C# classes under `Assets/Scripts/Systems` and `State`; UI only reads state and calls systems.
- Content lives in ScriptableObjects (`Assets/Scripts/Data`). Saved state stores ids, never Unity object references.
- All times are game-time seconds (`GameState.gameTime`), so offline progress works by advancing that clock.
- Player actions return `ActionResult`; state changes raise `GameEvents`.
- Use Unity 6 APIs (`FindFirstObjectByType`, not `FindObjectOfType`). New projects use the Input System package, so do not use `UnityEngine.Input`.
- Work on a branch and open a PR. New scripts can be committed without `.meta` files; Unity generates them when Luca opens the project, and he commits them.
