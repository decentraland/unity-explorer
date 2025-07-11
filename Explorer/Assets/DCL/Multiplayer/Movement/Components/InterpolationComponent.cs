﻿using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Settings;

namespace DCL.Multiplayer.Movement
{
    public struct InterpolationComponent
    {
        public NetworkMovementMessage Start;
        public NetworkMovementMessage End;

        public float Time;
        public float TotalDuration;

        public InterpolationType SplineType;
        public bool UseMessageRotation;

        public bool Enabled { get; private set; }

        public float Present => Start.timestamp + Time;

        public void Restart(NetworkMovementMessage from, NetworkMovementMessage to, InterpolationType interpolationType, ICharacterControllerSettings settings)
        {
            SplineType = interpolationType;

            Start = from;
            End = to;

            Time = 0f;
            TotalDuration = End.timestamp - Start.timestamp;

            End.animState.MovementBlendValue = AnimationMovementBlendLogic.CalculateBlendValue(TotalDuration, Start.animState.MovementBlendValue,
                End.movementKind, End.velocitySqrMagnitude, settings);

            End.animState.SlideBlendValue = AnimationSlideBlendLogic.CalculateBlendValue(TotalDuration, Start.animState.SlideBlendValue, End.isSliding, settings);

            UseMessageRotation = true;
            Enabled = true;
        }

        public void Stop()
        {
            Enabled = false;
        }
    }
}
