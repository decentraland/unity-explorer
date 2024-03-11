using DCL.CharacterMotion.Components;
using System.Collections.Generic;

namespace DCL.Multiplayer.Movement.Settings
{
    public interface IMultiplayerMovementSettings
    {
        List<SendRuleBase> SendRules { get; set; }

        int InboxCount { get; set; }
        int PassedMessages { get; set; }
        int PackageLost { get; set; }
        bool StartSending { get; set; }
        float PackagesJitter { get; set; }
        float Latency { get; set; }
        float LatencyJitter { get; set; }
        float MinPositionDelta { get; set; }
        float MinTeleportDistance { get; set; }

        RemotePlayerInterpolationSettings InterpolationSettings { get; }

        bool useExtrapolation { get; }
        RemotePlayerExtrapolationSettings ExtrapolationSettings { get; }

        MovementKind LastMove { get; set; }
        bool LastJump { get; set; }

        int SamePositionTeleportFilterCount { get; set; }
    }
}
