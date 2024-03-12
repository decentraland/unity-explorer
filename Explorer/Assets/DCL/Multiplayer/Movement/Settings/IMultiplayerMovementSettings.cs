using DCL.CharacterMotion.Components;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement.Settings
{
    public interface IMultiplayerMovementSettings
    {
        List<SendRuleBase> SendRules { get; set; }

        int InboxCount { get; set; }

        // TEST NETWORK
        float Latency { get; set; }
        float LatencyJitter { get; set; }

        // TELEPORTATION
        float MinPositionDelta { get; set; }
        float MinTeleportDistance { get; set; }

        int SkipOldMessagesBatch { get; set; }
        int SkipSamePositionBatch { get; set; }

        // INTERPOLATION
        RemotePlayerInterpolationSettings InterpolationSettings { get; }

        // EXTRAPOLATION
        bool UseExtrapolation { get; }
        RemotePlayerExtrapolationSettings ExtrapolationSettings { get; }


    }
}
