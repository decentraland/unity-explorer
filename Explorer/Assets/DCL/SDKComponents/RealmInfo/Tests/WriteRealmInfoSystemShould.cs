using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utilities;
using ECS;
using ECS.TestSuite;
using LiveKit.Rooms;
using LiveKit.Rooms.Info;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DCL.SDKComponents.RealmInfo.Tests
{
    public class WriteRealmInfoSystemShould : UnitySystemTestBase<WriteRealmInfoSystem>
    {
        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private IRealmData realmData;
        private IRoomHub roomHub;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            realmData = Substitute.For<IRealmData>();
            realmData.RealmName.Returns("R'lyeh");
            realmData.CommsAdapter.Returns("Innsmouth");
            realmData.NetworkId.Returns(238);
            realmData.IsDirty.Returns(true);
            realmData.Configured.Returns(true);
            IIpfsRealm ipfsRealm = Substitute.For<IIpfsRealm>();
            ipfsRealm.CatalystBaseUrl.Returns(URLDomain.FromString("https://yog/sothot/content/"));
            realmData.Ipfs.Returns(ipfsRealm);

            roomHub = Substitute.For<IRoomHub>();
            IRoom islandRoom = Substitute.For<IRoom>();
            IRoomInfo roomInfo = Substitute.For<IRoomInfo>();
            roomInfo.Sid.Returns("femiwofmiewofjSID");
            islandRoom.Info.Returns(roomInfo);
            roomHub.IslandRoom().Returns(islandRoom);

            ObjectProxy<IRoomHub> roomHubProxy = new ObjectProxy<IRoomHub>();
            roomHubProxy.SetObject(roomHub);

            system = new WriteRealmInfoSystem(world, ecsToCRDTWriter, realmData, roomHubProxy);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void WriteRealmInfoDataCorrectly()
        {
            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBRealmInfo, (IRealmData realmData, IRoomHub roomHub)>>(),
                                SpecialEntitiesID.SCENE_ROOT_ENTITY,
                                Arg.Is<(IRealmData realmData, IRoomHub roomHub)>(data =>
                                    data.realmData.RealmName == realmData.RealmName
                                    && data.realmData.CommsAdapter == realmData.CommsAdapter
                                    && data.realmData.NetworkId == realmData.NetworkId
                                    && data.realmData.Ipfs.CatalystBaseUrl == realmData.Ipfs.CatalystBaseUrl
                                    && data.roomHub.IslandRoom().Info.Sid == roomHub.IslandRoom().Info.Sid));
        }

        [Test]
        public void WriteRealmInfoOnlyWhenRealmDataIsDirty()
        {
            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBRealmInfo, (IRealmData realmData, IRoomHub roomHub)>>(),
                                SpecialEntitiesID.SCENE_ROOT_ENTITY,
                                Arg.Is<(IRealmData realmData, IRoomHub roomHub)>(data =>
                                    data.realmData.RealmName == realmData.RealmName
                                    && data.realmData.CommsAdapter == realmData.CommsAdapter
                                    && data.realmData.NetworkId == realmData.NetworkId
                                    && data.realmData.Ipfs.CatalystBaseUrl == realmData.Ipfs.CatalystBaseUrl
                                    && data.roomHub.IslandRoom().Info.Sid == roomHub.IslandRoom().Info.Sid));
            ecsToCRDTWriter.ClearReceivedCalls();

            realmData.RealmName.Returns("Decentraland");
            realmData.CommsAdapter.Returns("adapterland");
            realmData.NetworkId.Returns(238);
            realmData.Ipfs.CatalystBaseUrl.Returns(URLDomain.FromString("https://shub/niggurath/content/"));
            roomHub.IslandRoom().Info.Sid.Returns("dcLr32egsSID");

            realmData.IsDirty.Returns(false);
            system.Update(0);

            ecsToCRDTWriter.DidNotReceiveWithAnyArgs()
                           .PutMessage(
                                Arg.Any<Action<PBRealmInfo, (IRealmData realmData, IRoomHub roomHub)>>(),
                                Arg.Any<CRDTEntity>(),
                                Arg.Any<(IRealmData realmData, IRoomHub roomHub)>());

            realmData.IsDirty.Returns(true);
            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBRealmInfo, (IRealmData realmData, IRoomHub roomHub)>>(),
                                SpecialEntitiesID.SCENE_ROOT_ENTITY,
                                Arg.Is<(IRealmData realmData, IRoomHub roomHub)>(data =>
                                    data.realmData.RealmName == realmData.RealmName
                                    && data.realmData.CommsAdapter == realmData.CommsAdapter
                                    && data.realmData.NetworkId == realmData.NetworkId
                                    && data.realmData.Ipfs.CatalystBaseUrl == realmData.Ipfs.CatalystBaseUrl
                                    && data.roomHub.IslandRoom().Info.Sid == roomHub.IslandRoom().Info.Sid));
        }

        [Test]
        public void WriteRealmInfoDataOnInitializeRegardlessOfIsDirtyFlag()
        {
            realmData.IsDirty.Returns(false);
            system.Initialize();

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBRealmInfo, (IRealmData realmData, IRoomHub roomHub)>>(),
                                SpecialEntitiesID.SCENE_ROOT_ENTITY,
                                Arg.Is<(IRealmData realmData, IRoomHub roomHub)>(data =>
                                    data.realmData.RealmName == realmData.RealmName
                                    && data.realmData.CommsAdapter == realmData.CommsAdapter
                                    && data.realmData.NetworkId == realmData.NetworkId
                                    && data.realmData.Ipfs.CatalystBaseUrl == realmData.Ipfs.CatalystBaseUrl
                                    && data.roomHub.IslandRoom().Info.Sid == roomHub.IslandRoom().Info.Sid));
        }
    }
}
