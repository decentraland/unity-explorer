using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Systems;
using ECS.Abstract;
using System;
using UnityEngine;

namespace DCL.Character.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ApplyCinemachineCameraInputSystem))]
    [UpdateAfter(typeof(ControlCinemachineVirtualCameraSystem))]
    public partial class UpdateCinemachineBrainSystem : BaseUnityLoopSystem
    {
        // The golden paths exist only in an armed capture session; an unarmed session never reads
        // the AppDomain slot.
        private static readonly bool GOLDEN_ARMED = Environment.GetEnvironmentVariable("DCL_PLAZABENCH_DETERM_CLOCK") == "1";

        // Pre-snap brain output, published as "golden.cameraRaw" for the golden camera pose dump;
        // one allocation, refreshed in place every frozen frame.
        private static readonly double[] frozenPoseRaw = new double[7];

        private int frozenUpdates;

        public UpdateCinemachineBrainSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ManualBrainUpdateQuery(World);
        }

        [Query]
        private void ManualBrainUpdate(ref ICinemachinePreset cinemachinePreset)
        {
            // While the golden freeze holds, the brain updates with dt=0 and a FreeLook's composer
            // and collider converge on previous-frame history; invalidating the previous state each
            // frame makes every update a snap computed only from the axes and the follow target.
            bool goldenFrozen = GOLDEN_ARMED && (AppDomain.CurrentDomain.GetData("golden.frozen") as bool?) == true;

            if (goldenFrozen && cinemachinePreset.Brain.ActiveVirtualCamera is CinemachineVirtualCameraBase vcam)
                vcam.PreviousStateIsValid = false;

            cinemachinePreset.Brain.ManualUpdate();

            if (goldenFrozen)
                SnapFrozenPose(cinemachinePreset.Brain);
        }

        /// <summary>
        ///     The frozen output pose is a pure function of the rig axes and the follow target, but
        ///     its last bits pass through libm and rig arithmetic that need not agree between boots
        ///     or platforms, and the rasterizer snaps vertices to 1/256 px, so a last-bit pose
        ///     difference flips scattered edge pixels frame-wide. Snapping the pose the brain wrote
        ///     onto <see cref="GoldenPoseGrid"/> collapses every sub-grid difference onto one
        ///     canonical pose. The raw pose and the live rig state are published for the golden
        ///     camera pose dump so any residual can be located on either side of the snap.
        /// </summary>
        private void SnapFrozenPose(CinemachineBrain brain)
        {
            Camera cam = brain.OutputCamera;

            if (cam == null)
                return;

            Transform t = cam.transform;
            Vector3 p = t.position;
            Quaternion q = t.rotation;

            frozenPoseRaw[0] = p.x;
            frozenPoseRaw[1] = p.y;
            frozenPoseRaw[2] = p.z;
            frozenPoseRaw[3] = q.x;
            frozenPoseRaw[4] = q.y;
            frozenPoseRaw[5] = q.z;
            frozenPoseRaw[6] = q.w;

            t.SetPositionAndRotation(GoldenPoseGrid.SnapPosition(p), GoldenPoseGrid.SnapRotation(q));

            frozenUpdates++;

            if (frozenUpdates == 1)
                AppDomain.CurrentDomain.SetData("golden.cameraRaw", frozenPoseRaw);

            // The rig line is rebuilt sparsely: the frozen state does not change between frames.
            if (frozenUpdates <= 2 || frozenUpdates % 64 == 0)
                AppDomain.CurrentDomain.SetData("golden.cameraRig", DescribeRig(brain));
        }

        private string DescribeRig(CinemachineBrain brain)
        {
            var sb = new System.Text.StringBuilder(1024);
            sb.Append($"rig frozenUpdates={frozenUpdates}");

            if (!(brain.ActiveVirtualCamera is CinemachineVirtualCameraBase vcam))
                return sb.Append(" vcam=none").ToString();

            CameraState s = vcam.State;
            sb.Append($" vcam={vcam.Name} type={vcam.GetType().Name} rawPos={Fmt(s.RawPosition)} rawRot={Fmt(s.RawOrientation)} posCorr={Fmt(s.PositionCorrection)} rotCorr={Fmt(s.OrientationCorrection)} lookAt={Fmt(s.ReferenceLookAt)} lensFov={s.Lens.FieldOfView:R}");

            if (vcam.Follow != null)
                sb.Append($" follow={vcam.Follow.name}@{Fmt(vcam.Follow.position)}");

            if (vcam.LookAt != null)
                sb.Append($" target={Fmt(vcam.LookAt.position)}");

            if (vcam is CinemachineFreeLook freeLook)
            {
                sb.Append($" xAxis={freeLook.m_XAxis.Value:R} yAxis={freeLook.m_YAxis.Value:R}");

                for (var i = 0; i < 3; i++)
                {
                    CinemachineVirtualCamera rig = freeLook.GetRig(i);

                    if (rig != null)
                        sb.Append($" rig{i}={Fmt(rig.State.RawPosition)}");
                }
            }

            var collider = vcam.GetComponent<CinemachineCollider>();

            if (collider != null)
                sb.Append($" colliderDisplacement={collider.GetCameraDisplacementDistance(vcam):R}");

            return sb.ToString();
        }

        private static string Fmt(Vector3 v) => $"{v.x:R},{v.y:R},{v.z:R}";

        private static string Fmt(Quaternion q) => $"{q.x:R},{q.y:R},{q.z:R},{q.w:R}";
    }
}
