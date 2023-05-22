# unity-explorer

Explorer PoC

# Goals

- Implement the current Decentraland protocol
- Execute an SDK7 scene
- Dynamically load the world

# Protocol Generation
## Update protocol

To update the protocol to the last version of the protocol, you can execute the following commands:
```
cd scripts
npm install @dcl/protocol@next
npm run build-protocol
```

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

![img.png](D:\Decentraland\unity-explorer\ReadmeResources\img.png)

## Control scene lifecycle

![img_1.png](D:\Decentraland\unity-explorer\ReadmeResources\img_1.png)

At the moment one scene can be active at a time. By default at startup no scene is launched.

- When a new scene is selected, the old scene is unloaded releasing its components to the pool. 
- "Stop" just disposes the scene
- You can easily notice if the resources are not properly disposed by looking at the Unity hierarchy.
- Setting the framerate can be useful for a stress test.
