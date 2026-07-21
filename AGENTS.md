# AGENTS.md

Unity 6 (**6000.0.70f1**, URP, new Input System) arcade kart game — university exam project. Comments, tooltips, folder and layer names are in Italian; keep that style in existing files.

## Verify changes

No tests, no CI, no asmdefs. The only check is a batch-mode compile (verified working, ~1 min):

```bash
~/Unity/Hub/Editor/6000.0.70f1/Editor/Unity -batchmode -quit -projectPath "$PWD" -logFile -
```

Exit 0 = compiles. Editor 6000.0.**71**f1 is also installed — do NOT use it, it would upgrade the project version. `Library/` is local and gitignored; first run on a fresh clone reimports everything (slow).

## Layout

- `Assets/00.Project/` uses numbered folders: `00.Common` (shared), `01.Menu` (empty scaffolding), `02.Gameplay` (the actual work). Subfolders follow `01.Scene` / `02.Script` / `03.Asset`.
- `00.Common/Prof/` — professor's toolkit (scripts, docs, example scenes). Reference material; student code goes in `02.Gameplay/02.Script/`.
- **Real working scene: `Assets/00.Project/02.Gameplay/01.Scene/TestCAR/Test1.unity`** — it is NOT in Build Settings (only the unused `SampleScene.unity` is).
- Input actions asset: `00.Common/InputSystem_Actions.inputactions`.

## Gotchas

- **File/class name mismatches** (don't "fix" them): `CarController1sos.cs` → class `KartController`, `TorreKart.cs` → `KartCollectedStack`, `PickupOGG.cs` → `Pickup`, `triggersCAM.cs` → `CameraPhaseTrigger`. Scene references bind by script GUID + class name, so renaming a **class** breaks scenes; renaming the file (with its `.meta`) is safe. New MonoBehaviours, however, must have matching file/class names or they can't be added in the editor.
- Custom layers: `ground` (6), `Vehicle` (7), `Muris` (8), `Oggeto` (9). The typos are wired into scenes — use them verbatim. `KartController.groundLayer` must be set to `ground` only.
- Toolkit triggers filter by the `Player` tag on the kart; pickups then look for `KartCollectedStack` in the player hierarchy.
- `Prof/Docs/README.md` describes the full original toolkit and links files that don't exist here (`LevelManager.cs`, `HUDController.cs`, a `Core/KartController.cs`). Trust the actual `Scripts/` folders, not doc links.
- Namespaces: `ArcadeKart.Core` / `.Gameplay` / `.Behaviors` / `.Utility`.
- Every new file/folder under `Assets/` needs its `.meta` committed alongside it.
