# Quality Settings

`Quality Settings` Asset unites every required setting that's normally scattered around different menus and assets in Unity.

![image](https://github.com/decentraland/unity-explorer/assets/118179774/35cdb588-c430-44ce-b2b7-1adc70789eb4)

`Quality` Debug Widget provides a capability to change the quality level at runtime and modify individual pre-scripted settings.

![image](https://github.com/decentraland/unity-explorer/assets/118179774/acc5be6a-0149-4cc6-b62a-3c0a75c6478c)

## Default Quality Settings

The settings that you can find at `Project Settings` are drawn in our asset to keep everything in one place.

<img width="650" alt="image" src="https://github.com/decentraland/unity-explorer/assets/118179774/e4e0f201-9118-4574-ae68-5cdf18d24e4c">

## Renderer Features

Some settings such as `Ambient Occlusion` are represented by a renderer feature.

In Unity, it's already possible to have different `Renderers` per quality level.

In `Quality Settings` a shortcut to them is drawn so it's possible to modify them without going down all the references.

The same asset will be modified so all the changes are reflected automatically.

<img width="650" alt="image" src="https://github.com/decentraland/unity-explorer/assets/118179774/02adc847-3da4-4a57-93d0-3c435bd5b292">

You can make changes to the `RendererFeatures` and they will be reflected automatically at runtime.

At runtime there is a possibility to toggle every possible renderer feature. However, Unity has a natural limitation: every feature should be added at the Editor time, it can't be added at runtime but it can be switched on/off.

Currently, it's provided by the `Debug View` but in the future, it's easy to connect to the prod-ready flow (e.g. Settings View)

![image](https://github.com/decentraland/unity-explorer/assets/118179774/4ef84d69-096c-4fbb-86b4-c11849bb0e97)

## Volume Profile

A lot of Post-Processing effects are provided by [the Volume feature of URP](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/Volumes.html). The full list of the built-in effects you can find at https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/EffectList.html

`Quality Settings` support different sets of `VolumeComponents` per quality level.

![image](https://github.com/decentraland/unity-explorer/assets/118179774/e01edc73-2017-42b0-86c0-bf9196a3c9b1)

The system automatically maintains a global volume on the scene and modifies it accordingly on quality level selection.

![image](https://github.com/decentraland/unity-explorer/assets/118179774/b70e40f9-d1f4-4246-9364-8bf738dc5526)

![image](https://github.com/decentraland/unity-explorer/assets/118179774/383d345d-8054-425a-aaf1-944cdd2d9c32)

You can make changes to the `VolumeProfile` and they will be reflected automatically at runtime.

## Lens Flare

Presets for `Lens Flare` are automatically generated for each quality level, and, thus, can be changed individually.

![image](https://github.com/decentraland/unity-explorer/assets/118179774/e5ace78d-cadb-4a18-8eca-d7003c548a22)

The system automatically maintains an instance of the prefab if `Lens Flare` is enabled.

![image](https://github.com/decentraland/unity-explorer/assets/118179774/957ddb30-5d25-42ae-8cb8-a4482fbba518)

However, the configuration of the `Lens Flare (SRP)` is stored on the component, not on the asset. Thus, if you modify the prefab itself you will need to switch the quality level to reflect the changes.

![image](https://github.com/decentraland/unity-explorer/assets/118179774/0be8e3a0-3f82-48e4-a31b-8f72ee24e996)

## Simple Fog

Provides a possibility to control `Fog` per quality level.

![image](https://github.com/decentraland/unity-explorer/assets/118179774/8f203f3f-5406-4ca4-bce6-a5b3a976e36c)

Fog requires support from the shader side. If it's not supported then Fog will not affect such assets at all.

### Custom Behavior at Editor Time

If `Update In Editor` is ticked then all the aforementioned behavior is applied to the currently open scene automatically.

![image](https://github.com/decentraland/unity-explorer/assets/118179774/9492b387-e68c-436e-911a-aef640fce5c7)

If the game is launched from the `Main` scene it's always applied at runtime.
