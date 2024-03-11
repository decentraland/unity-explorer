using DCL.CharacterMotion.Components;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement.Settings
{
    public interface IMultiplayerMovementSettings
    {
        List<SendRuleBase> SendRules { get; set; }

        int InboxCount { get; set; }

        float Latency { get; set; }
        float LatencyJitter { get; set; }
        float MinPositionDelta { get; set; }
        float MinTeleportDistance { get; set; }

        RemotePlayerInterpolationSettings InterpolationSettings { get; }

        bool useExtrapolation { get; }
        RemotePlayerExtrapolationSettings ExtrapolationSettings { get; }

        MovementKind LastMove { get; set; }
        bool LastJump { get; set; }

        int SkipOldMessagesBatch { get; set; }
        int SkipSamePositionBatch { get; set; }
    }
}
