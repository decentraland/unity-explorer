using System.Collections.Generic;

namespace DCL.Multiplayer.Movement.Settings
{
    public interface IMultiplayerMovementSettings
    {
        List<SendRuleBase> SendRules { get; set; }

        int InboxCount { get; set; }

        // TEST NETWORK
        bool SelfSending { get; set; }
        float Latency { get; set; }
        float LatencyJitter { get; set; }

        // TELEPORTATION
        float MinPositionDelta { get; set; }
        float MinTeleportDistance { get; set; }

        // INTERPOLATION
        RemotePlayerInterpolationSettings InterpolationSettings { get; }

        // EXTRAPOLATION
        bool UseExtrapolation { get; }
        RemotePlayerExtrapolationSettings ExtrapolationSettings { get; }
    }
}
