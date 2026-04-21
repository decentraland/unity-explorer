using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Web3;
using ECS.Abstract;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER)]
    public partial class DebugPulseSystem : BaseUnityLoopSystem
    {
        private readonly IPulseMultiplayerService service;
        private readonly ITransport transport;
        private readonly PulseMultiplayerBus multiplayerBus;
        private readonly ElementBinding<string>? transportState;
        private readonly ElementBinding<string>? authenticated;
        private readonly ElementBinding<string>? bytesSentPerSec;
        private readonly ElementBinding<string>? bytesReceivedPerSec;
        private readonly ElementBinding<string>? pktsSentPerSec;
        private readonly ElementBinding<string>? pktsReceivedPerSec;
        private readonly ElementBinding<string>? resyncsLastMinute;
        private readonly ElementBinding<string>? emoteMismatchesLastMinute;
        private readonly ElementBinding<string>? emoteIdDisplay;
        private readonly DebugWidgetVisibilityBinding visibilityBinding = new (true);
        private readonly bool enabled;

        private long previousBytesSent;
        private long previousBytesReceived;
        private long previousPktsSent;
        private long previousPktsReceived;
        private long resyncCountAtWindowStart;
        private long emoteMismatchCountAtWindowStart;
        private float elapsed;
        private float windowElapsed;

        private DebugPulseSystem(
            World world,
            IPulseMultiplayerService service,
            ITransport transport,
            PulseMultiplayerBus multiplayerBus,
            IDebugContainerBuilder debugBuilder) : base(world)
        {
            this.service = service;
            this.transport = transport;
            this.multiplayerBus = multiplayerBus;

            DebugWidgetBuilder? widget = debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.PULSE);
            enabled = widget != null;

            if (!enabled)
                return;

            transportState = new ElementBinding<string>(string.Empty);
            authenticated = new ElementBinding<string>(string.Empty);
            bytesSentPerSec = new ElementBinding<string>("0 B/s");
            bytesReceivedPerSec = new ElementBinding<string>("0 B/s");
            pktsSentPerSec = new ElementBinding<string>("0 pkt/s");
            pktsReceivedPerSec = new ElementBinding<string>("0 pkt/s");
            resyncsLastMinute = new ElementBinding<string>("0");
            emoteMismatchesLastMinute = new ElementBinding<string>("0");
            emoteIdDisplay = new ElementBinding<string>(string.Empty);

            widget!
               .SetVisibilityBinding(visibilityBinding)
               .AddCustomMarker("Transport State", transportState)
               .AddCustomMarker("Authenticated", authenticated)
               .AddCustomMarker("Bytes Sent", bytesSentPerSec)
               .AddCustomMarker("Bytes Received", bytesReceivedPerSec)
               .AddCustomMarker("Pkts Sent", pktsSentPerSec)
               .AddCustomMarker("Pkts Received", pktsReceivedPerSec)
               .AddCustomMarker("Resyncs (last 60s)", resyncsLastMinute)
               .AddCustomMarker("Emote Mismatches (last 60s)", emoteMismatchesLastMinute)
               .AddStringFieldWithConfirmation(string.Empty, "Lookup Peer Emote", ShowPeerEmote)
               .AddCustomMarker("Is Emoting?", emoteIdDisplay);
        }

        protected override void Update(float t)
        {
            if (!enabled || !visibilityBinding.IsConnectedAndExpanded)
                return;

            transportState!.SetAndUpdate(transport.State.ToString());
            authenticated!.SetAndUpdate(service.IsAuthenticated.ToString());

            elapsed += t;
            windowElapsed += t;

            resyncsLastMinute!.SetAndUpdate((multiplayerBus.ResyncCount - resyncCountAtWindowStart).ToString());
            emoteMismatchesLastMinute!.SetAndUpdate((multiplayerBus.EmoteStateMismatchCount - emoteMismatchCountAtWindowStart).ToString());

            if (windowElapsed >= 60f)
            {
                resyncCountAtWindowStart = multiplayerBus.ResyncCount;
                emoteMismatchCountAtWindowStart = multiplayerBus.EmoteStateMismatchCount;
                windowElapsed = 0f;
            }

            if (elapsed >= 1f)
            {
                long bytesSentDelta = transport.BytesSent - previousBytesSent;
                long bytesReceivedDelta = transport.BytesReceived - previousBytesReceived;
                long pktsSentDelta = transport.PacketsSent - previousPktsSent;
                long pktsReceivedDelta = transport.PacketsReceived - previousPktsReceived;

                bytesSentPerSec!.SetAndUpdate(FormatBytes(bytesSentDelta / elapsed));
                bytesReceivedPerSec!.SetAndUpdate(FormatBytes(bytesReceivedDelta / elapsed));
                pktsSentPerSec!.SetAndUpdate($"{pktsSentDelta / elapsed:F0} pkt/s");
                pktsReceivedPerSec!.SetAndUpdate($"{pktsReceivedDelta / elapsed:F0} pkt/s");

                previousBytesSent = transport.BytesSent;
                previousBytesReceived = transport.BytesReceived;
                previousPktsSent = transport.PacketsSent;
                previousPktsReceived = transport.PacketsReceived;
                elapsed = 0f;
            }
        }

        private void ShowPeerEmote(string wallet) =>
            emoteIdDisplay!.SetAndUpdate(multiplayerBus.IsPeerEmoting(new Web3Address(wallet)) ? "Emoting" : "Not emoting");

        private static string FormatBytes(float bytesPerSec)
        {
            return bytesPerSec switch
            {
                >= 1_048_576f => $"{bytesPerSec / 1_048_576f:F2} MB/s",
                >= 1024f => $"{bytesPerSec / 1024f:F1} KB/s",
                _ => $"{bytesPerSec:F0} B/s",
            };
        }
    }
}
