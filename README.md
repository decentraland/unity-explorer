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
- [ ] `AudioSource`
- [ ] `AudioStream`
- [ ] `AvatarAttach`
- [ ] `AvatarModifierArea`
- [ ] `AvatarShape`
- [ ] `Billboard`
- [ ] `CRDT_MESSAGE_HEADER_LENGTH`
- [ ] `CameraMode`
- [ ] `CameraModeArea`
- [x] `EngineInfo`
- [ ] `GltfContainer`
- [ ] `GltfContainerLoadingState`
- [x] `Material`
- [x] `MeshCollider`
- [x] `MeshRenderer`
- [ ] `Name`
- [ ] `NftShape`
- [ ] `PointerEvents`
- [ ] `PointerEventsResult`
- [ ] `PointerLock`
- [ ] `RESERVED_LOCAL_ENTITIES`
- [ ] `RESERVED_STATIC_ENTITIES`
- [x] `Raycast`
- [ ] `RaycastResult`
- [ ] `SYSTEMS_REGULAR_PRIORITY`
- [ ] `SyncComponents`
- [ ] `TextShape`
- [x] `Transform`
- [ ] `Tween`
- [ ] `TweenSequence`
- [ ] `TweenState`
- [ ] `UiBackground`
- [ ] `UiCanvasInformation`
- [ ] `UiDropdown`
- [ ] `UiDropdownResult`
- [ ] `UiInput`
- [ ] `UiInputResult`
- [ ] `UiText`
- [ ] `UiTransform`
- [ ] `VideoEvent`
- [ ] `VideoPlayer`
- [ ] `VisibilityComponent`
- [ ] `componentDefinitionByName`
- [ ] `engine`
- [ ] `inputSystem`
- [ ] `pointerEventsSystem`
- [ ] `raycastSystem`
- [ ] `tweenSystem`
- [ ] `videoEventsSystem`


## Runtime API
- [ ] `CommsApi`
- [ ] `CommunicationsController`
- [ ] `EngineApi`
- [ ] `EnvironmentApi`
- [ ] `EthereumController`
- [ ] `Players`
- [ ] `PortableExperiences`
- [ ] `RestrictedActions`
- [x] `Runtime`
- [x] `Scene`
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
