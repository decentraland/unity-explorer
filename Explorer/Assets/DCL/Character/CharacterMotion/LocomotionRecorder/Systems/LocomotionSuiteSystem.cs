using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using DCL.RealmNavigation;
using DCL.Utility;
using ECS;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Arch;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>
    ///     Plays the locomotion suite: spawns the player, settles, runs each scenario and records every rendered frame.
    ///     <para>
    ///         Runs after the character moved (<see cref="ChangeCharacterPositionGroup" />), the camera updated
    ///         (<see cref="CameraGroup" />) and the character rotated (<see cref="RotateCharacterSystem" />), so a sample holds
    ///         this frame's final pose. Velocity comes from the fixed ticks that ran this frame, which consumed the
    ///         previous frame's input; the recorded input is the one this frame's input systems produced.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RotateCharacterSystem))]
    [LogCategory(ReportCategory.MCP)]
    public partial class LocomotionSuiteSystem : BaseUnityLoopSystem
    {
        private readonly LocomotionSuiteSession session;
        private readonly Entity playerEntity;
        private readonly ILoadingStatus loadingStatus;
        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;

        private SingleInstanceEntity camera;
        private SingleInstanceEntity physicsTick;

        internal LocomotionSuiteSystem(World world, LocomotionSuiteSession session, Entity playerEntity, ILoadingStatus loadingStatus, IScenesCache scenesCache,
            IRealmData realmData) : base(world)
        {
            this.session = session;
            this.playerEntity = playerEntity;
            this.loadingStatus = loadingStatus;
            this.scenesCache = scenesCache;
            this.realmData = realmData;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            physicsTick = World.CachePhysicsTick();
        }

        protected override void Update(float t)
        {
            switch (session.Phase)
            {
                case LocomotionSuitePhase.WaitingForWorld:
                    WaitForWorld();
                    break;
                case LocomotionSuitePhase.Spawning:
                    session.LockFrameRate(session.Scenario.Fps);
                    FinishSpawn();
                    break;
                case LocomotionSuitePhase.Settling:
                    session.LockFrameRate(session.Scenario.Fps);
                    Settle();
                    break;
                case LocomotionSuitePhase.Armed:
                case LocomotionSuitePhase.Running:
                    session.LockFrameRate(session.Scenario.Fps);

                    // The input seam starts the run in the frame's input phase; this covers a frame in which it did not run.
                    session.TryStartRun();

                    if (session.Phase == LocomotionSuitePhase.Running)
                        Record();

                    break;
            }
        }

        private void WaitForWorld()
        {
            if (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed
                || World.Has<PlayerTeleportIntent>(playerEntity)
                || World.Has<PlayerTeleportIntent.JustTeleported>(playerEntity)
                || !World.Get<CharacterRigidTransform>(playerEntity).IsGrounded)
            {
                session.PhaseStartTime = -1;
                return;
            }

            if (session.PhaseStartTime < 0)
                session.PhaseStartTime = UnityEngine.Time.timeAsDouble;

            if (UnityEngine.Time.timeAsDouble - session.PhaseStartTime < LocomotionSuiteSession.WORLD_READY_DELAY)
                return;

            CaptureOrigin();
            BeginRun();
        }

        /// <summary>Fixes the scene coordinates and the spawn point for the whole suite.</summary>
        private void CaptureOrigin()
        {
            Vector3 playerPosition = World.Get<CharacterTransform>(playerEntity).Position;
            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            session.SceneOrigin = scene?.SceneData.Geometry.BaseParcelPosition ?? Vector3.zero;
            session.SceneBaseParcel = scene?.SceneData.SceneShortInfo.BaseParcel ?? Vector2Int.zero;
            session.Spawn = session.Options.Spawn ?? playerPosition - session.SceneOrigin;
            session.SpawnYaw = session.Options.SpawnYaw;

            Vector2Int parcel = playerPosition.ToParcel();

            session.PositionDescription = scene != null
                ? $"{realmData.RealmName}: scene '{scene.SceneData.SceneShortInfo.Name}' base {session.SceneBaseParcel.x},{session.SceneBaseParcel.y}, parcel {parcel.x},{parcel.y}"
                : $"{realmData.RealmName}: no scene, parcel {parcel.x},{parcel.y}";
        }

        private void BeginRun()
        {
            LocomotionScenario scenario = session.Scenario;
            session.LockFrameRate(scenario.Fps);

            Vector3 spawn = session.SceneOrigin + session.Spawn;

            // The instant teleport scenes use (movePlayerTo): it resolves collisions and platform state like any teleport.
            World.AddOrSet(playerEntity, new PlayerTeleportIntent(null, spawn.ToParcel(), spawn, CancellationToken.None, isPositionSet: true));

            session.Phase = LocomotionSuitePhase.Spawning;
            session.SetStatus($"{scenario.Id} r{session.Repeat}: spawning");
        }

        private void FinishSpawn()
        {
            if (World.Has<PlayerTeleportIntent>(playerEntity) || World.Has<PlayerTeleportIntent.JustTeleported>(playerEntity))
                return;

            LocomotionScenario scenario = session.Scenario;
            float yaw = session.SpawnYaw ?? scenario.CameraYaw;
            Vector3 facing = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            // Every run starts from the same state: no residual velocity from the previous run (smooth damping never reaches exact zero).
            CharacterRigidTransform rigidTransform = World.Get<CharacterRigidTransform>(playerEntity);
            rigidTransform.MoveVelocity = default(CharacterRigidTransform.MovementVelocity);
            rigidTransform.ExternalVelocity = Vector3.zero;
            rigidTransform.ExternalImpulse = Vector3.zero;
            rigidTransform.LookDirection = facing;
            World.Get<CharacterTransform>(playerEntity).Transform.rotation = Quaternion.LookRotation(facing);

            session.PhaseStartTime = UnityEngine.Time.timeAsDouble;
            session.Phase = LocomotionSuitePhase.Settling;
            session.SetStatus($"{scenario.Id} r{session.Repeat}: settling");
        }

        private void Settle()
        {
            LocomotionScenario scenario = session.Scenario;
            double settled = UnityEngine.Time.timeAsDouble - session.PhaseStartTime;

            if (settled < scenario.Settle)
                return;

            if (!World.Get<CharacterRigidTransform>(playerEntity).IsGrounded && settled < scenario.Settle + LocomotionSuiteSession.MAX_EXTRA_SETTLE)
                return;

            if (session.Options.InputSeam == LocomotionInputSeam.Keyboard && LocomotionKeyboardInput.EditorWithholdsKeyboard())
            {
                if (!session.WaitingForFocus)
                {
                    ReportHub.LogWarning(GetReportData(), "Locomotion suite paused: the Game view is not focused, so the Editor withholds keyboard input from the game. Click into the Game view (or run with input component)");
                    session.WaitingForFocus = true;
                    session.SetStatus($"{scenario.Id} r{session.Repeat}: paused, focus the Game view");
                }

                return;
            }

            session.WaitingForFocus = false;
            session.ArmedFrame = UnityEngine.Time.frameCount;
            session.Phase = LocomotionSuitePhase.Armed;
            session.SetStatus($"{scenario.Id} r{session.Repeat}: recording");
        }

        private void Record()
        {
            LocomotionScenario scenario = session.Scenario;
            float runTime = session.RunTime();

            CharacterRigidTransform rigidTransform = World.Get<CharacterRigidTransform>(playerEntity);
            ref readonly MovementInputComponent input = ref World.Get<MovementInputComponent>(playerEntity);
            Transform characterTransform = World.Get<CharacterTransform>(playerEntity).Transform;

            // The avatar model is parented to the character; read it when present so the yaw is the one on screen.
            Transform avatarTransform = World.TryGet(playerEntity, out AvatarBase? avatarBase) && avatarBase != null ? avatarBase.transform : characterTransform;

            session.Frames.Add(new LocomotionFrameSample
            {
                T = runTime,
                Dt = UnityEngine.Time.deltaTime,
                Fwd = input.Axes.y,
                Right = input.Axes.x,
                Kind = input.Kind,
                Pos = characterTransform.position - session.SceneOrigin,
                Vel = rigidTransform.MoveVelocity.Velocity,
                ExternalVel = rigidTransform.ExternalVelocity,
                GravityVelY = rigidTransform.GravityVelocity.y,
                Grounded = rigidTransform.IsGrounded,
                YawAvatar = Yaw(avatarTransform.forward),
                YawCamera = Yaw(camera.GetCameraComponent(World).Camera.transform.forward),
                PhysicsTick = physicsTick.GetPhysicsTickComponent(World).Tick,
                Expected = scenario.SampleAt(runTime),
            });

            if (runTime >= scenario.Duration - LocomotionScenario.TIME_TOLERANCE)
                FinishRun();
        }

        private void FinishRun()
        {
            LocomotionScenario scenario = session.Scenario;

            var run = new LocomotionRunInfo(
                session.Repeat,
                session.AnimationsEnabled,
                session.PositionDescription,
                session.RunStartedUtc,
                session.Options.InputSeam,
                session.Options.FixedDeltaTime,
                UnityEngine.Time.fixedDeltaTime,
                camera.GetCameraComponent(World).Mode.ToString(),
                session.SceneBaseParcel,
                session.Spawn,
                session.SpawnYaw ?? scenario.CameraYaw);

            try
            {
                LocomotionTraceSummary file = LocomotionTraceWriter.Write(session.Options.OutputDirectory, scenario, in run, session.Frames);
                session.AddWrittenFile(in file);

                if (file.InputMismatchFrames > 0)
                    ReportHub.LogWarning(GetReportData(), $"Locomotion trace {file.Path}: {file.InputMismatchFrames} of {file.Frames} frames consumed input other than the scenario's");
                else
                    ReportHub.Log(LogType.Log, GetReportData(), $"Locomotion trace written: {file.Path} ({file.Frames} frames)");
            }
            catch (Exception e)
            {
                session.AddError($"the trace was not written: {e.Message}");
                ReportHub.LogException(e, GetReportData());
            }

            session.Frames.Clear();

            if (session.AdvanceRun())
                BeginRun();
            else
                FinishSuite();
        }

        private void FinishSuite()
        {
            session.Complete();
            session.SetStatus($"done: {session.ScenarioCount} scenarios x {session.Options.Repeats} in {session.Options.OutputDirectory}");
            ReportHub.Log(LogType.Log, GetReportData(), $"Locomotion suite complete: {session.ScenarioCount} scenarios x {session.Options.Repeats} runs in {session.Options.OutputDirectory}");

            if (session.Options.QuitWhenDone)
                ExitUtils.Exit();
        }

        /// <summary>Trace-format yaw: degrees, 0 = facing +z (north), positive clockwise seen from above (towards +x), in (-180, 180].</summary>
        internal static float Yaw(Vector3 forward)
        {
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            return yaw <= -180f ? yaw + 360f : yaw;
        }
    }
}
