using DCL.Diagnostics;
using LiveKit.Proto;
using LiveKit.Rooms.Info;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogRoomInfo : IRoomInfo
    {
        private readonly IRoomInfo origin;

        public ConnectionState ConnectionState
        {
            get
            {
                ConnectionState connectionState = origin.ConnectionState;
                ReportHub
                   .WithReport(ReportCategory.LIVEKIT)
                   .Log($"LogRoomInfo: ConnectionState: {connectionState}");
                return connectionState;
            }
        }

        public string Sid
        {
            get
            {
                string sid = origin.Sid;
                ReportHub
                   .WithReport(ReportCategory.LIVEKIT)
                   .Log($"LogRoomInfo: Sid: {sid}");
                return sid;
            }
        }

        public string Name
        {
            get
            {
                string name = origin.Name;
                ReportHub
                   .WithReport(ReportCategory.LIVEKIT)
                   .Log($"LogRoomInfo: Name: {name}");
                return name;
            }
        }

        public string Metadata
        {
            get
            {
                string metadata = origin.Metadata;
                ReportHub
                   .WithReport(ReportCategory.LIVEKIT)
                   .Log($"LogRoomInfo: Metadata: {metadata}");
                return metadata;
            }
        }

        public LogRoomInfo(IRoomInfo origin)
        {
            this.origin = origin;
        }
    }
}
