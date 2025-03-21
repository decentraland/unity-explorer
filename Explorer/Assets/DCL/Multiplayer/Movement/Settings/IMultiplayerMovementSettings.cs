﻿using DCL.CharacterMotion.Components;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement.Settings
{
    public interface IMultiplayerMovementSettings
    {
        bool UseCompression { get; set; }

        List<SendRuleBase> SendRules { get; set; }

        int InboxCount { get; set; }

        float MoveSendRate{ get; }
        float StandSendRate{ get; }

        float[] VelocityTiers { get; }

        // TELEPORTATION
        float MinPositionDelta { get; }
        float MinRotationDelta { get; }
        float MinTeleportDistance { get; }

        // INTERPOLATION
        RemotePlayerInterpolationSettings InterpolationSettings { get; }

        // EXTRAPOLATION
        bool UseExtrapolation { get; }
        RemotePlayerExtrapolationSettings ExtrapolationSettings { get; }
        float AccelerationTimeThreshold { get; }
        float IdleSlowDownSpeed { get; }
        Dictionary<MovementKind, float> MoveKindByDistance { get; }
    }
}
