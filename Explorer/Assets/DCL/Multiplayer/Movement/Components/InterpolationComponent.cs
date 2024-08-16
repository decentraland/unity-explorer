using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Settings;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public struct InterpolationComponent
    {
        public NetworkMovementMessage Start;
        public NetworkMovementMessage End;

        public float Time;
        public float TotalDuration;

        public InterpolationType SplineType;
        public bool Enabled { get; private set; }

        public void Restart(NetworkMovementMessage from, NetworkMovementMessage to, InterpolationType interpolationType, ICharacterControllerSettings settings)
        {
            SplineType = interpolationType;

            Start = from;
            End = to;

            Time = 0f;
            TotalDuration = End.timestamp - Start.timestamp;

            int movementBlendId = AnimationMovementBlendLogic.GetMovementBlendId(to.velocity.sqrMagnitude, to.movementKind);

            to.animState.MovementBlendValue = AnimationMovementBlendLogic.CalculateBlendValue(TotalDuration, from.animState.MovementBlendValue,
                movementBlendId, to.movementKind, to.velocity.magnitude, settings);

            to.animState.SlideBlendValue = AnimationSlideBlendLogic.CalculateBlendValue(TotalDuration, from.animState.SlideBlendValue, to.isSliding, settings);

            Enabled = true;
        }

        public void Stop()
        {
            Enabled = false;
        }
    }
}
