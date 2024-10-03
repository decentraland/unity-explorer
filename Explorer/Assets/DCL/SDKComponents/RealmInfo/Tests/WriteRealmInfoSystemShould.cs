using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Utilities;
using ECS;
using ECS.TestSuite;
using LiveKit.Rooms;
using LiveKit.Rooms.Info;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
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

            ISceneData? sceneData = Substitute.For<ISceneData>();
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition());

            IGateKeeperSceneRoom? gateKeeperSceneRoom = Substitute.For<IGateKeeperSceneRoom>();
            gateKeeperSceneRoom.IsSceneConnected(Arg.Any<string>()).Returns(true);
            gateKeeperSceneRoom.CurrentState().Returns(IConnectiveRoom.State.Running);

            roomHub.SceneRoom().Returns(gateKeeperSceneRoom);

            ObjectProxy<IRoomHub> roomHubProxy = new ObjectProxy<IRoomHub>();
            roomHubProxy.SetObject(roomHub);

            system = new WriteRealmInfoSystem(world, ecsToCRDTWriter, realmData, roomHubProxy, sceneData);
        }

        [TearDown]
        public override void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void WriteRealmInfoDataCorrectly()
        {
            system.Update(0);

            AssertPutMessageReceived();
        }

        private void AssertPutMessageReceived()
        {
            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBRealmInfo, (IRealmData realmData, WriteRealmInfoSystem.CommsRoomInfo roomInfo)>>(),
                                SpecialEntitiesID.SCENE_ROOT_ENTITY,
                                Arg.Is<(IRealmData realmData, WriteRealmInfoSystem.CommsRoomInfo roomInfo)>(data =>
                                    data.realmData.RealmName == realmData.RealmName
                                    && data.realmData.CommsAdapter == realmData.CommsAdapter
                                    && data.realmData.NetworkId == realmData.NetworkId
                                    && data.realmData.Ipfs.CatalystBaseUrl == realmData.Ipfs.CatalystBaseUrl
                                    && data.roomInfo.IslandSid == roomHub.IslandRoom().Info.Sid
                                    && data.roomInfo.IsConnectedSceneRoom));
        }

        private void AssertPutMessageDidNotReceive()
        {
            ecsToCRDTWriter.DidNotReceiveWithAnyArgs()
                           .PutMessage(
                                Arg.Any<Action<PBRealmInfo, (IRealmData realmData, WriteRealmInfoSystem.CommsRoomInfo roomInfo)>>(),
                                Arg.Any<CRDTEntity>(),
                                Arg.Any<(IRealmData realmData, WriteRealmInfoSystem.CommsRoomInfo roomInfo)>());
        }

        [Test]
        public void WriteRealmInfoOnlyWhenRealmDataIsDirty()
        {
            system.Update(0);

            AssertPutMessageReceived();

            ecsToCRDTWriter.ClearReceivedCalls();

            realmData.RealmName.Returns("Decentraland");
            realmData.CommsAdapter.Returns("adapterland");
            realmData.NetworkId.Returns(238);
            realmData.Ipfs.CatalystBaseUrl.Returns(URLDomain.FromString("https://shub/niggurath/content/"));

            realmData.IsDirty.Returns(false);
            system.Update(0);

            AssertPutMessageDidNotReceive();

            realmData.IsDirty.Returns(true);
            system.Update(0);

            AssertPutMessageReceived();
        }

        [Test]
        public void WriteRealmInfoWhenCommsRoomInfoChanged()
        {
            system.Update(0);

            AssertPutMessageReceived();

            ecsToCRDTWriter.ClearReceivedCalls();

            roomHub.IslandRoom().Info.Sid.Returns("dcLr32egsSID");

            realmData.IsDirty.Returns(false);

            system.Update(0);

            AssertPutMessageReceived();
        }

        [Test]
        public void WriteRealmInfoDataOnInitializeRegardlessOfIsDirtyFlag()
        {
            realmData.IsDirty.Returns(false);
            system.Initialize();

            AssertPutMessageReceived();
        }
    }
}
