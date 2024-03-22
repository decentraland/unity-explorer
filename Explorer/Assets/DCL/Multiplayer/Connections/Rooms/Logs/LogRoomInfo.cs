using DCL.Diagnostics;
using LiveKit.Proto;
using LiveKit.Rooms.Info;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogRoomInfo : IRoomInfo
    {
        private readonly IRoomInfo origin;
        private readonly Action<string> log;

        public ConnectionState ConnectionState
        {
            get
            {
                ConnectionState connectionState = origin.ConnectionState;
                log($"LogRoomInfo: ConnectionState: {connectionState}");
                return connectionState;
            }
        }

        public string Sid
        {
            get
            {
                string sid = origin.Sid;
                log($"LogRoomInfo: Sid: {sid}");
                return sid;
            }
        }

        public string Name
        {
            get
            {
                string name = origin.Name;
                log($"LogRoomInfo: Name: {name}");
                return name;
            }
        }

        public string Metadata
        {
            get
            {
                string metadata = origin.Metadata;
                log($"LogRoomInfo: Metadata: {metadata}");
                return metadata;
            }
        }

        public LogRoomInfo(IRoomInfo origin) : this(origin, ReportHub.WithReport(ReportCategory.LIVEKIT).Log) { }

        public LogRoomInfo(IRoomInfo origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }
    }
}
