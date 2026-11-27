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

----

## Locomotion recorder

`Character/CharacterMotion/LocomotionRecorder` records how the own player moves, frame by frame, for comparison with other clients. It plays scenarios (JSON: a step function of W/A/S/D, walk, sprint and jump keys over time, a held camera yaw, a frame rate, a settle time and a duration) and writes one JSON Lines trace per run: a header, one line per rendered frame (time, delta time, the movement and jump input consumed, position in scene coordinates, movement velocity, grounded, avatar and camera yaw) and a footer. Launch flags: [App Arguments § `locomotion-*`](app-arguments.md#locomotion-); a debug panel widget, `Locomotion: Recorder`, toggles the avatar animations, re-runs the latest suite and cancels it.

In a running client the MCP server starts suites without a restart: `run_locomotion_suite`, `get_locomotion_status`, `cancel_locomotion_suite` and `set_avatar_animations` ([MCP Automation § Locomotion recorder](mcp-automation.md#locomotion-recorder-debug-only)). The recorder exists only with the debug flag (Editor, development build or `--debug`), and then when a `--locomotion-*` flag asks for it or the MCP server is on. Created for the MCP tools alone it is inert until a suite starts: no input device, no frame-rate lock, avatar animations on. The input seam, frame time, spawn, output and animations are chosen per suite; a suite's `animations` setting holds while it runs and the toggle applies again after it. Each suite gets a run id; the status lists each written trace with its footer counts (`dropped`, `input_mismatch_frames`).

Per run: the player is teleported to the spawn point (the instant teleport `movePlayerTo` uses), its movement velocity is zeroed and it faces the spawn yaw; it then stays idle for the settle time with the frame rate locked (`Application.targetFrameRate`, vsync off) and the third-person camera held at the scenario's yaw. The next frame is t = 0.

Scenario format 2 adds an optional camera track, `"camera": [{"t", "yaw", "pitch"}]` (yaw in degrees clockwise from north; times non-negative and ordered; pitch in [-90, 90] on every sample or on none), so a recorded free play, keys and camera, can be replayed. Format 1 refuses a track. During the run the camera's yaw follows the track by run time, interpolated the short way round between samples and held at the first sample before it and at the last after it; the setup holds the yaw at t = 0, which is also the spawn facing without a spawn yaw. Pitch is checked but not played: the orbit's Y axis is a 0..1 position across three rigs, not an angle, and movement uses only the flattened camera forward, so pitch does not change the path. A tracked run's header adds `extra.camera_control: "track"` and each frame `extra.cam.yaw`, the yaw the track holds; `yaw_camera` stays the rendered camera's. Replay in third person: other camera modes are not held.

| Part | Where it runs |
|---|---|
| Keys | `keyboard` seam: a virtual keyboard (W/A/S/D, Shift, Ctrl, Space) whose state is queued in `InputSystem.onBeforeUpdate`, so `UpdateInputMovementSystem` and `UpdateInputJumpSystem` read it in the same frame through the DCLInput actions. In the Editor the Game view must have focus, or the Editor keeps keyboard input for itself; the run waits until it has (the status reports `waitingForFocus`). Created by the first keyboard suite. `component` seam: `LocomotionComponentInputSystem` overwrites `MovementInputComponent` after `UpdateInputMovementSystem` and `JumpInputComponent` after `UpdateInputJumpSystem` (the same jump gates; a press arms the trigger for the next fixed tick, as the action's `WasPressedThisFrame` does); needs no focus. |
| Camera yaw | `LocomotionCameraYawSystem`, between `ApplyCinemachineCameraInputSystem` and `UpdateCinemachineBrainSystem`: sets the world-space orbit's X axis. |
| Recording | `LocomotionSuiteSystem`, after `RotateCharacterSystem`: the frame's final position and rotations; with animations on, the own avatar's Animator after this frame's animation update. |
| Animations off | `LocomotionAnimationsOffSystem`, after `FinishAvatarMatricesCalculationSystem`: keeps the own avatar's Animator disabled, so the model holds its pose. |

Timing to keep in mind when reading a trace: `CalculateCharacterVelocitySystem` runs on the fixed tick (`PhysicsSystemGroup`), before the frame's input systems, so a frame's velocity comes from the previous frame's input and changes only on frames with a fixed tick (`extra.physics_tick`); `InterpolateCharacterSystem` moves the character by that velocity every rendered frame. A jump pressed in a frame starts on the next fixed tick, or later within the jump grace time if the character is not yet grounded. A frame's `in.jump` is `JumpInputComponent.IsPressed`; `UpdateInputJumpSystem` leaves it unchanged while a scene lock or the double jump and gliding features gate the jump button (in the air with both features off, for instance), so a release there counts in `input_mismatch_frames`. The frame's `extra` adds `jump_triggered` (a jump started on a fixed tick since the previous frame), `jump_count` and `glide`; the header's `extra` states whether `double_jump` and `gliding` are on.

With animations on, each frame's `extra` also holds the own avatar's Animator, base layer, read after this frame's animation update (the presentation group runs at the end of PreLateUpdate, after the Animator), in the layout the Bevy recorder and the offline replay of `CharacterAnimator.controller` share: `anim`, one entry per clip, `{clip, w, t, speed}` (`AnimationClip.name`; `AnimatorClipInfo.weight` as the Animator reports it, which during a transition already includes the state's share, a clip in both states summed; the state's normalized time as a phase, the fractional part for a looping clip and held below 1 after a non-looping one ends; `speed × speedMultiplier`); `anim_params`, the parameters `MovementBlend`, `SlideBlend`, `JumpCount`, `IsGrounded`, `IsFalling`, `IsLongFall`, `IsStunned` and `GlideBlend` as the Animator holds them (set on the fixed tick by `CharacterAnimationSystem`); `anim_state`, `{state, next, progress}` named from a table of the controller's states (`#hash` for any other), the raw normalized times `nt` and `next_nt`, the reported clip weight sums `cur_w` and `next_w` (they add up to 1 in a transition between states with clips) and `cur_empty: true` when the current state reports no clip weight: an interrupted transition, whose frozen pose Unity does not report as clips, or a state without a motion, so the frame's weights sum to less than 1; `anim_layers`, the weights of the `RightPointAtHand` (`point`) and `Rotation` layers. A state without a motion (`Jog_Stop`) reports no clip, so its frames hold `anim: []`. A frame whose Animator did not run writes `anim: null` alone, and the header adds `extra.anim_source` and `extra.anim_weights: "animator"` (the weights are the Animator's own; traces without this key multiplied them by the transition share a second time). Runs with animations off write none of these keys.

The recorder logs under `ReportCategory.MCP`, which the development and the production `ReportsHandlingSettings` both enable at every severity, so a player build's `Player.log` holds each suite's start, every trace written, failed loads and writes, cancellation and completion. Without `--locomotion-out` (or the tool's `out`), traces go to `<persistentDataPath>/locomotion-traces/unity/`; on Windows that is `%USERPROFILE%\AppData\LocalLow\Decentraland\Explorer\locomotion-traces\unity\`, next to `Player.log`. The Editor renders slower than a player, so measure high frame rates in a player build: a release build with `--debug`, or a development build.
