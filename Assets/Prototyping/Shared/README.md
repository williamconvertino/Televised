# Shared Prototyping Assets

Scripts and assets used by every prototype under `Assets/Prototyping/`. Each prototype folder (such as
`Movement/`) only holds its own scenes, test scripts and notes, and builds on what's here.

| Folder | Contents |
|---|---|
| `Scripts/Player` | Eyeball player: motor, input and jump resolvers, grapple, eye visuals, `MovementTuning` |
| `Scripts/Surfaces` | `Surface2D` paths, sensor, attachment controller |
| `Scripts/Generation` | Rectangle, ellipse and organic blob surface shapes |
| `Scripts/Legs` | Cosmetic procedural leg rig |
| `Scripts/Debug` | Debug lines, tuning panel, sandbox camera, spawn points |
| `Scripts/Editor` | `PrototypeAssetBuilder` (generates the assets below) and the blob inspector |
| `Prefabs` | `Player/EyeballPlayer`, `Surfaces/RectSurface`, `EllipseSurface`, `OrganicBlob` |
| `Settings` | `DefaultMovementTuning.asset`, the movement config the player prefab uses |
| `Materials`, `Sprites` | `PrototypeUnlit.mat` and `Circle.png` |

## Using it in a new prototype

- Drop `Prefabs/Player/EyeballPlayer` into a scene, and add surfaces with **GameObject ▸ Prototyping ▸ Surfaces**.
- Editor scene builders can call `PrototypeAssetBuilder.EnsureAssets`, `Instance`, `Configure` and `Set`, the
  same way `Movement/Scripts/Editor/PlaytestLevelBuilder.cs` does.
- The code is in the `Televised.Prototyping.Shared` namespace (editor tools in `Televised.Prototyping.Shared.EditorTools`).

## Regenerating

**Prototyping ▸ Shared ▸ Rebuild Prefabs + Assets** overwrites the prefabs, and creates the sprite and
material if they are missing. `DefaultMovementTuning.asset` is never overwritten.
**Prototyping ▸ Movement ▸ Rebuild Sandbox** also rebuilds these prefabs, so hand edits to them are lost.
