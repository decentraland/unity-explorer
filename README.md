# unity-explorer

Explorer renderer 

# Protocol Generation
## Update protocol

To update the protocol to the last version of the protocol, you can execute the following commands:
```
cd scripts
npm install @dcl/protocol@next
npm run build-protocol
```

## SDK7 Integration Progress

### Components 
- [ ] `Animator`
- [x] `AudioSource`
- [x] `AudioStream`
- [ ] `AvatarAttach`
- [ ] `AvatarModifierArea`
- [x] `AvatarShape`
- [x] `Billboard`
- [x] `CRDT_MESSAGE_HEADER_LENGTH`
- [ ] `CameraMode`
- [ ] `CameraModeArea`
- [x] `EngineInfo`
- [x] `GltfContainer`
- [x] `GltfContainerLoadingState`
- [x] `Material`
- [x] `MeshCollider`
- [x] `MeshRenderer`
- [x] `Name`
- [x] `NftShape`
- [x] `PointerEvents`
- [x] `PointerEventsResult`
- [ ] `PointerLock`
- [ ] `RESERVED_LOCAL_ENTITIES`
- [ ] `RESERVED_STATIC_ENTITIES`
- [x] `Raycast`
- [x] `RaycastResult`
- [ ] `SYSTEMS_REGULAR_PRIORITY`
- [ ] `SyncComponents`
- [x] `TextShape`
- [x] `Transform`
- [x] `Tween`
- [x] `TweenSequence`
- [ ] `TweenState`
- [x] `UiBackground`
- [x] `UiCanvasInformation`
- [x] `UiDropdown`
- [x] `UiDropdownResult`
- [x] `UiInput`
- [x] `UiInputResult`
- [x] `UiText`
- [x] `UiTransform`
- [x] `VideoEvent`
- [x] `VideoPlayer`
- [x] `VisibilityComponent`

## Runtime API
- [ ] `CommsApi`
- [ ] `CommunicationsController`
- [x] `EngineApi`
- [ ] `EnvironmentApi`
- [ ] `EthereumController`
- [ ] `Players`
- [ ] `PortableExperiences`
- [ ] `RestrictedActions`
- [x] `Runtime` (partially)
- [ ] `Scene`
- [ ] `SignedFetch`
- [ ] `Testing`
- [ ] `UserActionModule`
- [ ] `UserIdentity`

## Regenerate protocol

Just run:
```
cd scripts
npm run build-protocol
```

# Test scenes
## Add a new scene
To be able to select the scene at runtime
- Place it to the "StreamingAssets\Scenes" directory.
- Add its name without an extension to the "Scenes" list on the `EntryPoint` component.

![img.png](ReadmeResources/img.png)

## Control scene lifecycle

![img_1.png](ReadmeResources/img_1.png)

At the moment one scene can be active at a time. By default at startup no scene is launched.

- When a new scene is selected, the old scene is unloaded releasing its components to the pool. 
- "Stop" just disposes the scene
- You can easily notice if the resources are not properly disposed by looking at the Unity hierarchy.
- Setting the framerate can be useful for a stress test.
