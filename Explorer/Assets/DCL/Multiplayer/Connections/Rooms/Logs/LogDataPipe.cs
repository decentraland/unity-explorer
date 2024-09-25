using DCL.Diagnostics;
using LiveKit.Proto;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogDataPipe : IDataPipe
    {
        private const string PREFIX = "LogDataPipe:";

        private readonly IDataPipe origin;

        public event ReceivedDataDelegate? DataReceived;

        public LogDataPipe(IDataPipe origin)
        {
            this.origin = origin;
            origin.DataReceived += OriginOnDataReceived;
        }

        private void OriginOnDataReceived(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{PREFIX} data received {data.Length} bytes from {participant.ReadableString()} - {kind}");
            DataReceived?.Invoke(data, participant, kind);
        }

        public void PublishData(Span<byte> data, string topic, IReadOnlyCollection<string> destinationSids, DataPacketKind kind = DataPacketKind.KindLossy)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{PREFIX} publish data {data.Length} bytes to {topic} - {string.Join(", ", destinationSids)} - {kind}");
            origin.PublishData(data, topic, destinationSids, kind);
        }
    }
}
