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
using System.Threading;

namespace DCL.Multiplayer.Profiles.RemoteAnnouncements
{
    public class ThreadSafeRemoteAnnouncements : IRemoteAnnouncements
    {
        private readonly IRoomHub roomHub;
        private readonly MessageParser<Packet> parser;
        private readonly IMultiPool multiPool;
        private readonly List<RemoteAnnouncement> list = new ();
        private readonly Semaphore semaphore = new (1, 1);

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
            this.roomHub.IslandRoom().DataPipe.DataReceived -= DataPipeOnDataReceived;
            this.roomHub.SceneRoom().DataPipe.DataReceived -= DataPipeOnDataReceived;
        }

        private void DataPipeOnDataReceived(ReadOnlySpan<byte> data, Participant participant, DataPacketKind kind)
        {
            //TODO deduplication

            if (TryParse(data, out var response) == false)
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

        public bool NewBunchAvailable()
        {
            semaphore.WaitOne();
            bool result = list.Count > 0;
            semaphore.Release();
            return result;
        }

        public OwnedBunch<RemoteAnnouncement> Bunch() =>
            new (semaphore, list);

        private void ThreadSafeAdd(RemoteAnnouncement remoteAnnouncement)
        {
            semaphore.WaitOne();
            list.Add(remoteAnnouncement);
            semaphore.Release();
        }
    }
}
