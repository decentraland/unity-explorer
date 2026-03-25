# Directories & Assemblies Structure

## Encapsulated Features

Every feature can contain an arbitrary range of `Assets` which can include but are not limited to:
- Scripts
- Textures
- Models (FBX, OBJ, etc.)
- Audio
- Prefabs
- Scriptable Objects

The default place for a feature is a subdirectory at `Assets/DCL` path.

In order to maintain a reasonable number of assemblies and a manageable number of dependencies between them consider the following rules:
- **Strive for large assemblies instead of splitting into small ones.** Avoid creating new `.asmdef` or `.asmref` files unless necessary — the folder or a parent folder may already be covered by an existing assembly definition.
- When a new assembly reference **is** needed, prefer `.asmref` ([Assembly Definition References](https://docs.unity3d.com/Manual/class-AssemblyDefinitionReferenceImporter.html)) to fold code into an existing large assembly rather than introducing a new `.asmdef`.
- Only create a dedicated `.asmdef` when the feature **must be referenced by other assemblies** or requires strict dependency isolation.
- Control exposed members by `public` access level: if members should not be exposed make them `internal` instead. It makes the difference when we pursue the minimum number of assemblies.
- ECS Systems **must always** use an `.asmref` pointing to `DCL.Plugins`.
- Use different directories for **Unit Tests** but connect all of them to the single "DCL.Tests" assembly via `.asmref`. We don't care about the number of dependencies in the case of Tests as any other assembly never references Tests.

## Pure technical implementations

Some features may include code only being purely technical ones. Among the currently implemented ones are:
- CRDT Protocol and Bridge
- Diagnostics Utilities
- General Utilities
- Scene Runtime
- SDK Components handling that results in code only without settings. However, it's still advised to migrate them to a separate fully-fledged feature to be more flexible and maintain a unified structure.
- Prioritization, Realm and Scenes lifecycle

In this case, the feature can be created inside the `Scripts` folder.
It's a less preferable way of structuring things: in the future, we will be taking more subjects out of there and placing them as an [encapsulated feature](#encapsulated-features).

Regarding distribution between assemblies, the same rules are applied:
- Merging several folders together can be even more aggressive.
- All Components related Tests should be connected with the single "ECS.Unity.Tests"

## Container, Plugins, and Global code

All containers and plugins belong to a "global" visibility level:
- "DCL.Plugins" is the only "global" assembly that can contain any number of references but should not be referenced itself (apart from Tests). Other "global" directories are connected to it by "Assembly Reference".
- Their Tests are still connected to the "DCL.Tests" assembly.
- Plugins can reference any types from any assemblies to execute logic on them but they should produce systems and dependencies without knowledge about unrelated assemblies. Thus, we maintain a limited number of references across features.

### Plugin file placement

- `<Feature>Plugin.cs` **must be placed in the `Systems/` folder** alongside the ECS systems it injects.
- If the plugin has **no ECS systems**, do not create a `Systems/` folder. Place the plugin directly in:
  - `Assets/DCL/PluginSystem/Global/` — for global plugins (`IDCLGlobalPlugin`)
  - `Assets/DCL/PluginSystem/World/` — for world plugins (`IDCLWorldPlugin`)
