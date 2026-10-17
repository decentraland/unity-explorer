# Emotes

## Summary

This page will give you a proper understanding of how DCL emotes work inside the Explorer.

## How They Worked in unity-renderer

To understand how emotes work, first, we need to understand how they worked in the past. At first, emotes were just a single animation clip, we downloaded a GLB file and just played all the clips contained within by using the Unity legacy Animation component.

Why were we using the Legacy Animation component? Because Unity does not let you create non-legacy animation clips in runtime for the Mecanim system. Why were we playing all the clips at the same time? Because user-generated content can sometimes be tricky to deal with, sometimes there are empty or unwanted clips as leftovers in the file.

Then the extended emotes feature came and we had to make changes. In order to detect which clip is for the Avatar and which clip is for the props, we had to implement a nomenclature for clip names ([documentation](https://docs.decentraland.org/creator/emotes/props-and-sounds/)).

## How They Work in unity-explorer

Since with the new Locomotion system the animation transition got complex enough, we also transitioned into using the Unity Animator system with Mecanim. This enabled us to have more control and easier configuration for more complex transitions.

This also came with a big drawback which was not being able to create animations in runtime when loading GLBs.

But since we are loading Asset Bundles in unity-explorer, we modified the AssetBundle generation for emotes as well. Now the AssetBundle contains an Animator Controller with a simple transition and a trigger with the same name as the clip (you can check the implementation [here](https://github.com/decentraland/asset-bundle-converter/blob/main/asset-bundle-converter/Assets/AssetBundleConverter/AssetBundleConverter.cs#L439-L489)).

When the emote is played, we get the clip with the avatar nomenclature and assign that to the Avatar's AnimatorController ([source](https://github.com/decentraland/unity-explorer/blob/main/Explorer/Assets/DCL/AvatarRendering/AvatarShape/UnityInterface/AvatarBase.cs#L119C1-L120C1)), this way we were able to transition any animation into an emote. And then we trigger the prop clip as well.

## Embedded Emotes

### Old Emotes

Since all emotes now need an AnimationController and most of our old embedded emotes were just a clip, we set up a script to convert all animation clips into their prefab version with an Animator, the same format as the AssetBundles. ([Source](https://github.com/decentraland/unity-explorer/blob/main/Explorer/Assets/DCL/AvatarRendering/Emotes/Editor/EmbeddedEmotesEditor.cs))

### New Emotes

If you are implementing a new emote and they are not integrated in the blockchain yet, please follow these steps:

- All new emotes are contained within `EmbeddedEmotes/ExtendedEmotes` folder.
- They have the same exact format as the AssetBundles.
- TODO: They could be an asset bundle. Ideally these emotes should be on the blockchain but for time constraints this has not been done yet.
- A script in the AssetBundle converter has been used to convert GLB files into this format very quickly. Ideally we should not use it ([PR](https://github.com/decentraland/asset-bundle-converter/pull/93)).
- Add the references to `EmbeddedEmotes` scriptable object.
- Update the `ExplorePanelPlugin.ExplorePanelSettings` at `Global Plugins Settings`.

![EmbeddedEmotes settings](https://github.com/decentraland/unity-explorer/assets/7646450/1d9991ee-7b9a-475b-8008-8988eab6aec7)

## Implementation

In `DCL/AvatarRendering/Emotes/Systems` you will find all the systems that make the emotes work.

Every Entity contains a `CharacterEmoteComponent` which tracks the current state of an emote.

Entities can receive a `CharacterEmoteIntent` component which will be consumed by `CharacterEmoteSystem`. If the emote has not been loaded yet, this intent will persist until the emote is played unless there has been an issue with the loading of the asset.

The `EmotePlayer` class is responsible for playing, stopping, and pool-handling every emote in the game. Since multiple avatars may own the same emote and play them at the same time, we need to instantiate the same emote multiple times but download it only once. That's why this domain implements the usage of `EmoteReferences` which is just a MonoBehaviour with references. It's being used as a key for the pools as well, so that's why the `CharacterEmoteComponent` keeps track of it.
