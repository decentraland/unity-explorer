using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Typing;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Utilities.Extensions;
using Decentraland.Kernel.Comms.Rfc4;
using Google.Protobuf;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Proto;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public class ThreadSafeRemoteAnnouncements : IRemoteAnnouncements
    {
        private readonly IRoomHub roomHub;
        private readonly MessageParser<Packet> parser;
        private readonly IMultiPool multiPool;
        private readonly HashSet<RemoteAnnouncement> list = new ();
        private readonly MutexSync mutex = new ();

        public ThreadSafeRemoteAnnouncements(IRoomHub roomHub, IMultiPool multiPool)
        {
            this.roomHub = roomHub;
            this.multiPool = multiPool;
            parser = new MessageParser<Packet>(multiPool.Get<Packet>);

            this.roomHub.IslandRoom().DataPipe.DataReceived += DataPipeOnDataReceived;
            this.roomHub.SceneRoom().DataPipe.DataReceived += DataPipeOnDataReceived;
        }

        ~ThreadSafeRemoteAnnouncements()
        {
            roomHub.IslandRoom().DataPipe.DataReceived -= DataPipeOnDataReceived;
            roomHub.SceneRoom().DataPipe.DataReceived -= DataPipeOnDataReceived;
        }

        private void DataPipeOnDataReceived(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            //TODO deduplication

            if (TryParse(data, out Packet? response) == false)
                return;

            if (response!.MessageCase is Packet.MessageOneofCase.ProfileVersion)
            {
                uint version = response.ProfileVersion!.ProfileVersion;
                string walletId = participant.Identity;
                ThreadSafeAdd(new RemoteAnnouncement((int)version, walletId));
            }

            multiPool.Release(response);
        }

        private bool TryParse(ReadOnlySpan<byte> data, out Packet? packet)
        {
            try
            {
                packet = parser.ParseFrom(data).EnsureNotNull();
                return true;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(
                    ReportCategory.ARCHIPELAGO_REQUEST,
                    $"Someone sent invalid packet: {data.Length} {data.HexReadableString()} {e}"
                );

                packet = null;
                return false;
            }
        }

        public OwnedBunch<RemoteAnnouncement> Bunch() =>
            new (mutex, list);

        private void ThreadSafeAdd(RemoteAnnouncement remoteAnnouncement)
        {
            using MutexSync.Scope _ = mutex.GetScope();
            list.Add(remoteAnnouncement);
        }
    }
}
