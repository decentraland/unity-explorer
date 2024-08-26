using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Settings;

namespace DCL.Multiplayer.Movement
{
    public struct InterpolationComponent
    {
        private const float EPSILON = 0.01f;

        public NetworkMovementMessage Start;
        public NetworkMovementMessage End;

        public float Time;
        public float TotalDuration;

        public InterpolationType SplineType;
        public bool Enabled { get; private set; }

        public void Restart(NetworkMovementMessage from, NetworkMovementMessage to, InterpolationType interpolationType, ICharacterControllerSettings settings)
        {
            SplineType = interpolationType;

            if (Start.velocity.sqrMagnitude < EPSILON)
                SplineType = InterpolationType.FullMonotonicHermite;

            Start = from;
            End = to;

            Time = 0f;
            TotalDuration = End.timestamp - Start.timestamp;

            int movementBlendId = AnimationMovementBlendLogic.GetMovementBlendId(End.velocity.sqrMagnitude, End.movementKind);

            End.animState.MovementBlendValue = AnimationMovementBlendLogic.CalculateBlendValue(TotalDuration, Start.animState.MovementBlendValue,
                movementBlendId, End.movementKind, End.velocity.magnitude, settings);

            End.animState.SlideBlendValue = AnimationSlideBlendLogic.CalculateBlendValue(TotalDuration, Start.animState.SlideBlendValue, End.isSliding, settings);

            Enabled = true;
        }

        public void Stop()
        {
            Enabled = false;
        }
    }
}
