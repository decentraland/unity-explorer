# Avatar Animation for Demos

You can animate random avatars using the Timeline to prepare in-editor demos. Currently it is possible to make avatars do the following actions:

* **Teleport**: It sets the position and the rotation of the avatar by copying a Transform. The main purpose of this is to let you play the timeline several times so every avatar starts always at a given position.
* **Move**: The avatar moves forward and turns to the left or to the right. If you want it to turn 180 degrees while walking, you should animate the rotation without translation, and then make it walk again, as the "turn" animation is not available. The avatar stops when the clip ends.
* **Play emote**: It is possible to make the avatar play any emote, it does not matter whether it is downloaded or not. It can be looped and will stop as soon as the clip ends.

https://github.com/user-attachments/assets/ef2c3937-1574-4e71-b6b7-e73708fa7342

## Working with the Timeline

### Timeline creation
First, you need to create a Timeline asset.

1. Open the Timeline window at Windows->Sequencing->Timeline. You will see the text that tells you to create a GameObject.
2. Create an empty GameObject in the scene (it does not matter if the editor is playing or not, although it may be preferable to do it while it is not and save the object in the scene, to avoid repeating the process).
3. Select the new object and you will see how the Timeline window changes, showing a button at its center.
4. Press the Create button and save the asset wherever you want. Now the window should show a timeline. A PlayableDirector component has been added to the GameObject.
5. In Inspector, **set Update Method to Manual**. The `PlayableDirectorUpdating` system will be in charge of getting the instance and updating it every frame.
6. Set Play On Awake to false.

![imagen](https://github.com/user-attachments/assets/69811528-e2ea-44e2-8492-d36b24f07c5f)

### Adding tracks and clips
At the top left corner of the Timeline windows you can see a cross. When you unfold it you can select DCL.AvatarAnimation->Avatar Track. That will create a new row with an empty field. There you can attach the `AvatarBase` of an existing avatar from the scene. Of course you will have to do this at runtime, every time you play the application. You can either drag and drop the instance from the scene hierarchy or click on the field and get it from the list. You can add as many avatar tracks as you want.

Once there is a track, you can add animation clips on it by right-clicking on the clips area and selecting "Add Avatar movement clip" or any other clip from the context menu. Clips cannot blend. By selecting a clip you can set its parameters in the Inspector. Read the tooltips to learn how they affect the animation (they may not appear while playing).

![imagen](https://github.com/user-attachments/assets/eb9feb31-4fb3-4a15-acdb-f0e553f92bb3)

## Important note
When you create the random avatars using the Debug panel, be sure you leave the `Network avatar` checkbox empty, otherwise some animations will not work.

## About the implementation
You can find most of the code in DCL/AvatarAnimation/Editor. It is only available in Editor obviously.
If you want to create a new type of clip, declare a new class that inherits from `PlayableAsset` and another one that inherits from `BaseAvatarPlayableBehaviour`.

Since all this stuff is not integrated in the ECS world, you need a special way to access its content, which is `GlobalWorld.ECSWorldInstance`.

The system `PlayableDirectorUpdatingSystem` is in charge of locating the only object in the scene that has a `PlayableDirector` component and updating it at the appropriate moment, depending on the order of execution of the system with respect to others. This had to be done because otherwise the changes performed by the clips may be overridden by the logic in some systems, as Unity updated the Timeline separately and we could not assure when.
