using DCL.AvatarRendering.AvatarShape.Components;
using DCL.CharacterCamera;
using UnityEngine;

namespace DCL.Chat.Teleport
{
    internal static class GotoTeleportPresentation
    {
        private const float EFFECT_TIMELINE_DURATION = 2.8f;
        private static readonly int AVATAR_ID = Shader.PropertyToID("_DCLTeleportAvatar");
        private static readonly int EFFECT_ID = Shader.PropertyToID("_DCLTeleportEffect");

        public static void ApplyDeparture(GotoTeleportState state, in AvatarCustomSkinningComponent skinning, in CameraComponent camera, float duration)
        {
            float elapsed = state.Elapsed * (EFFECT_TIMELINE_DURATION / duration);
            float charge = Mathf.SmoothStep(0f, 1f, elapsed / 0.8f);
            float dissolve = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.25f, 2.3f, elapsed));
            float lift = Mathf.SmoothStep(0f, 0.85f, elapsed / 1.6f) + (dissolve * dissolve * 3f);

            if (state.Elapsed >= duration)
                RefreshOrigin(state);

            ApplyAvatar(state, skinning, charge, dissolve, elapsed, lift);
            state.Trails.Apply(state.Origin, elapsed);

            Vector3 framingPosition = FramingPosition(state);
            float frameBlend = Mathf.SmoothStep(0f, 1f, elapsed / 0.65f);
            Vector3 position = Vector3.Lerp(state.CameraPosition, framingPosition, frameBlend);
            Quaternion framingRotation = Quaternion.LookRotation(state.Origin + (Vector3.up * (1.1f + lift)) - position);
            Quaternion skyRotation = Quaternion.Euler(-82f, framingRotation.eulerAngles.y, 0f);
            float skyBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 2.75f, elapsed));
            camera.Camera.transform.SetPositionAndRotation(position, Quaternion.Slerp(
                Quaternion.Slerp(state.CameraRotation, framingRotation, frameBlend), skyRotation, skyBlend));
        }

        public static void ApplyLanding(GotoTeleportState state, in AvatarCustomSkinningComponent skinning, ref CameraComponent camera, float progress)
        {
            RefreshOrigin(state);
            float dissolve = 1f - Mathf.SmoothStep(0f, 1f, progress / 0.6f);
            float charge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.95f, progress));
            float lift = Mathf.Lerp(3.85f, 0f, Mathf.SmoothStep(0f, 1f, progress / 0.8f));
            ApplyAvatar(state, skinning, charge, dissolve, state.Elapsed + state.LandingElapsed, lift);
            state.Trails.Apply(state.Origin, EFFECT_TIMELINE_DURATION * (1f - progress));

            Transform cameraTransform = camera.Camera.transform;
            Vector3 position = FramingPosition(state);
            Quaternion framingRotation = Quaternion.LookRotation(state.Origin + (Vector3.up * (1.1f + lift)) - position);
            Quaternion skyRotation = Quaternion.Euler(-82f, framingRotation.eulerAngles.y, 0f);
            Quaternion rotation = Quaternion.Slerp(skyRotation, framingRotation, Mathf.SmoothStep(0f, 1f, progress / 0.45f));

            // Cinemachine has already produced this frame's gameplay pose at the destination.
            float returnBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, progress));
            position = Vector3.Lerp(position, cameraTransform.position, returnBlend);
            rotation = Quaternion.Slerp(rotation, cameraTransform.rotation, returnBlend);
            cameraTransform.SetPositionAndRotation(position, rotation);

            if (progress >= 0.8f && camera.Mode == CameraMode.ThirdPerson)
                camera.Mode = state.CameraMode;

            if (camera.Mode == CameraMode.FirstPerson)
                camera.IsTransitioningToFirstPerson = true;
        }

        private static void ApplyAvatar(GotoTeleportState state, in AvatarCustomSkinningComponent skinning, float charge, float dissolve, float elapsed, float lift)
        {
            if (state.Avatar != null)
                state.Avatar.transform.localPosition = state.AvatarLocalPosition + (Vector3.up * lift);

            Shader.SetGlobalInteger(AVATAR_ID, skinning.VertsOutRegion.StartIndex);
            Shader.SetGlobalVector(EFFECT_ID, new Vector4(charge, dissolve, elapsed, 0f));
        }

        private static void RefreshOrigin(GotoTeleportState state)
        {
            if (state.Avatar == null) return;
            Transform parent = state.Avatar.transform.parent;
            state.Origin = parent != null ? parent.TransformPoint(state.AvatarLocalPosition) : state.AvatarLocalPosition;
        }

        private static Vector3 FramingPosition(GotoTeleportState state)
        {
            Vector3 backward = state.CameraRotation * Vector3.back;
            backward.y = 0f;
            if (backward.sqrMagnitude < 0.01f) backward = Vector3.back;
            return state.Origin + (Vector3.up * 1.2f) + (backward.normalized * 4.5f);
        }
    }
}
