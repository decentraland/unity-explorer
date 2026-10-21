using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using UnityEngine;

namespace DCL.Chat.Teleport
{
    internal sealed class GotoTeleportState
    {
        public readonly GotoTeleportTrails Trails;
        public bool Active;
        public bool OwnsMotionStop;
        public bool OwnsAnimatorSpeed;
        public bool EmoteFrozen;
        public float AnimatorSpeed;
        public float HighestHandPosition;
        public float HighestHandNormalizedTime;
        public float Elapsed;
        public bool Landing;
        public float LandingElapsed;
        public bool LandingEmoteStarted;
        public AnimationClip? EmoteClip;
        public int EmoteStateHash;
        public CameraMode CameraMode;
        public Vector3 CameraPosition;
        public Quaternion CameraRotation;
        public Vector3 Origin;
        public Vector3 AvatarLocalPosition;
        public AvatarBase? Avatar;
        public UniTaskCompletionSource? Departure;

        public GotoTeleportState(GotoTeleportTrails trails)
        {
            Trails = trails;
        }
    }
}
