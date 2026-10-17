# Landscape

![image](https://github.com/decentraland/unity-explorer/assets/7646450/2d21eb61-5da8-4ee9-984d-7d340b2eba72)

## Intro

The Landscape system generates a randomly generated terrain in runtime based on the empty parcels of Genesis City.
In the case of named Worlds, it generates a terrain based on the owned parcels and a border padding.

If you want to try out different landscape settings, you can go to the `InfiniteTerrain` scene and press `Generate`

## Noise Generation

The most critical part of terrain generation is setting up proper noise settings so each asset has its own rules.
The noise algorithm uses different noise types supported by Unity Jobs system ( PERLIN, SIMPLEX, CELLULAR ).

Here's more info about how we generate noise: https://medium.com/@yvanscher/playing-with-perlin-noise-generating-realistic-archipelagos-b59f004d8401

## How to Create a Noise Asset

In order to setup a noise asset you have to create a scriptable object called NoiseData by right-clicking on a folder and selecting `Create > Landscape > Noise Data`

![image](https://github.com/decentraland/unity-explorer/assets/7646450/6ab83913-55f9-4c14-99e8-67d531813eb7)

In the image above you can see the custom inspector of the `Noise Data` which is going to re-generate a texture in real-time every time you change any setting.

## Noise Variant

In order to re-use a Noise Data asset we created a Noise Variant asset which uses the settings created by another asset and just overrides the seed

![image](https://github.com/decentraland/unity-explorer/assets/7646450/b9d2b83f-c7b6-4182-a597-98319a506a53)

## Composite Noise

This is an even more complex noise that combines other noises and can also do some additional simple operations without noise

![image](https://github.com/decentraland/unity-explorer/assets/7646450/29902cea-c05d-47c8-b07b-58ff57a8a3d4)

## How to Use the Noise in Runtime

Since some noise values get re-used multiple times, we had to create a cache system for noise, so in order to get the noise working correctly and fast, you have to use the `NoiseGeneratorCache` class which receives any of the scriptable object variants (all implement `INoiseDataFactory`) and returns an instance of `INoiseGenerator`. You have to Dispose this class once you are done with all the noise generation.

`INoiseGenerator` entry point is `Schedule`, which receives a `NoiseDataPointer` and a `JobHandle` (which is optional), this function will schedule a `NoiseJob` that will create a Unity `IJobParallelFor` (or a bunch of them, depending on the complexity of the noise) and will return a JobHandle that can be awaited and Completed.

The `NoiseDataPointer` defines the size and position of the noise, if you need a 512x512 texture you just set up the size value as 512.
If you need more textures of the same noise but in different positions, it is recommended that the offsets are set-up as multipliers of the size, for example for a 2x2 grid of 512x512 textures you might want to Schedule 4 jobs with these settings:
- size = 512, offsetX = 0, offsetY = 0
- size = 512, offsetX = 512, offsetY = 0
- size = 512, offsetX = 0, offsetY = 512
- size = 512, offsetX = 512, offsetY = 512

To get the data you will need to access the `INoiseGenerator.GetResult(NoiseDataPointer)` all results are cached until the class is disposed.
The results are formatted in a 1 Directional array `NativeArray<float>`

Generated noise supports negative values, so they can be used to generate rivers and ponds, to see the desired results of height variance check the `Show Height` toggle which will show terrain-like colors where blue is ocean level, green is ground and it goes to red if there's a high mountain.

### Generating a Texture

Since the Unity's terrain API does not accept a texture as an input in multiple parts of it, the Noise generator only creates numerical values.

But if you want an example on how we create a texture for the Noise Data inspector you can check the `NoiseTextureGenerator` class which uses the same `INoiseGenerator` to generate the data and then uses a `RenderTexture` and a `ComputeBuffer` to create the texture.

> Check CS_NoiseTexture.compute for more information on how the texture is being colored.

## Terrain Generation

The Landscape system uses Unity's Terrain API, which is automatically set up based on the user settings.

![image](https://github.com/decentraland/unity-explorer/assets/7646450/56ef43cb-46db-4cb4-a736-0de909b5153b)

### TerrainGenerationData

This is the main asset used by the `TerrainGenerator` class.
This class contains all the configurable settings for the terrain and also the heightmap noise, materials, tree, and detail assets.

### LandscapeAsset

This class defines an asset for the terrain, it can be a tree with LODs (or whatever physical asset) or just a detail (which is commonly grass and flowers) that has no colliders.

## Empty Parcel Heightmaps

Since we don't want to have mountains on single 1x1 empty parcels, we came up with a system that defines the max height of an empty parcel based on how many empty parcels it has around it.

![image](https://github.com/decentraland/unity-explorer/assets/7646450/abdf2591-3aba-4c36-a7eb-1d0ad8e6670d)

In this example you can see that owned parcels are marked with an `x`, the empty parcel that is far away from the owned parcels will have a higher height than the others.

The base height of each point of the empty parcel is interpolated linearly toward the height of the neighboring parcels so they are properly blending. This is also multiplied by the noise of the heightmap.
