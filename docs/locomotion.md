# Locomotion

## Summary

This section will provide you with an overview of how the Locomotion works.

### Components and Dependencies

The current implementation of Locomotion is based on Unity's `CharacterController` component and Unity's `Animation Rigging` package for Inverse Kinematics.

### Settings

All the Locomotion and IK settings are centralized at the `CharacterControllerSettings` scriptable object.

### Features

The locomotion system features the following:
- Input handling
- Simple jump (tapping the jump button)
- Long jump (long press jump button)
- Coyote timer
- Sprint and Walk
- Stun (when falling from very high places)
- Sliding from slopes
- Stuck prevention from multiple slopes
- Network replication
- Platform movement
- Emotes
- Inverse Kinematics for:
  - Feet
  - Hands
  - Head

### Plugin

The Locomotion is being initialized at `CharacterMotionPlugin`

All the systems are composed of running and jumping can be found at:
- CalculateCharacterVelocitySystem
- InterpolateCharacterSystem
- RotateCharacterSystem
- CharacterAnimationSystem

For other Locomotion-related systems, check out the folder: `Assets/DCL/Character/Systems` for all the systems involved in the character manipulation.

### Setup

The locomotion movement and physics are being simulated by the Character Controller component in the `CharacterObject` prefab.

The Animator and IK setup is at the `AvatarBase` prefab

The CharacterAnimator asset contains the AnimatorController which has all the proper transitions, check the inner states for more information about the transitions.

![image](https://github.com/decentraland/unity-explorer/assets/7646450/37b93bc0-6bd0-428e-8b43-808eb5caf670)

### VERY IMPORTANT

The AvatarBase prefab has the Armature of a DCL avatar, the scale of the Armature GameObject must be `0.01` and a rotation of `90` on the X-axis since the avatars are big and rotated.

### Animation Clips

They are inside a folder in `AvatarRendering/AvatarShape/Assets/Animations/`

![image](https://github.com/decentraland/unity-explorer/assets/7646450/7e0a8839-15e6-4b1c-8c32-ae32d9e8f2cc)

To avoid having GLB files in the project we extracted them as separate assets.

----

## Inverse Kinematics

Our current IK system uses Unity's [Animation Rigging](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.0/manual/index.html) package.
It is very important for you to read the [official document](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.0/manual/RiggingWorkflow.html) to understand how the rigging system works.

### Setup

You can check the Rig hierarchy at the `AvatarBase` prefab

#### Feet IK

For the Feet IK to work properly we need to do a sphere cast downwards from the foot bone to place the feet in a proper position.
To achieve this we had to:
- Create a bone constraint that mimics the Feet' position BEFORE any IK is being applied and AFTER the animation is being solved, those are called `RightConstraint` and `LeftConstraint`
- Create a Two Bone IK Constraint (2BIK) that moves the bones towards a Target (`RightFootIK`, `LeftFootIK`)
- The Target of this 2BIK is called `SubTarget`, which is placed at the bone's original position, since that bone is already at "ground" level, we don't want to move it to the real ground level because our feet will end up below where it's standing.

![image](https://github.com/decentraland/unity-explorer/assets/7646450/54d24bf9-d12b-45ab-87ce-43f98616796e)

- The real target transform, which is at the floor level, is called Target. The SubTarget has a Parent Constraint that will move the transform toward this target but it's going to keep the offset that it already has. So this Transform can be placed exactly where we want to place our feet.

![image](https://github.com/decentraland/unity-explorer/assets/7646450/9b5f068b-54c6-4c69-b183-fbd130ee976b)

- The Target transforms also have a child transform which is the Hint part of the 2BIK, we have them parented to avoid wonky situations where the Hint is badly positioned and our knees end up upside-down.

![image](https://github.com/decentraland/unity-explorer/assets/7646450/8693857c-d454-4008-ba72-da1ee81683bf)

- `HipsConstraint` is also a transform mimic that we use to check where the hips are after the animations and before any IK
- `HipCorrectionConstraint` is a constraint that we use to "pull down" the character to place the feet where they should in situations where you are in a rock, your character controller is over the rock and one of your feet is "flying". If the distances are correct, we pull down the character from its hips so the foot that was "flying" is actually on the ground, and the other one is bent correctly.

#### Hands IK

- The hands use the same technique as the feet, we implemented a 2BIK with a Target and a SubTarget, since both hands have their bones with different rotations (z-axis forward and z-axis backwards) we solved the offsets by using a Constraint, so the Target transform can go towards any point without taking into consideration the offsets.

#### Head IK

The Head IK was separated into 2 different constraints since we needed to control exactly how the look works.

 - `HorizontalLookAt` is a Twist Chain constraint that uses a Root and a Tip target, this one solves the horizontal rotation of the head and spine.
 - `VerticalLookAt` Since this is below the game object hierarchy, this one is solved after the previous one, so this one solves the vertical look-at of the head and the neck in order to avoid bending the spine when looking down

### Implementation

All the IK systems are being handled and updated by the `HandsIKSystem`, `HeadIKSystem`, and `FeetIKSystem` respectively.
All weight speed ratios, twist limits, and offsets can be found in `CharacterControllerSettings`.
