// using Arch.Core;
// using CommunicationData.URLHelpers;
// using CRDT;
// using CrdtEcsBridge.ECSToCRDTWriter;
// using DCL.AvatarRendering.Wearables;
// using DCL.AvatarRendering.Wearables.Helpers;
// using DCL.ECSComponents;
// using DCL.Multiplayer.SDK.Components;
// using DCL.Multiplayer.SDK.Systems;
// using DCL.Optimization.Pools;
// using DCL.Profiles;
// using ECS.TestSuite;
// using NSubstitute;
// using NUnit.Framework;
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using WriteAvatarEquippedDataSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteAvatarEquippedDataSystem;
//
// namespace DCL.Multiplayer.SDK.Tests
// {
//     public class WriteAvatarEquippedDataSystemShould : UnitySystemTestBase<WriteAvatarEquippedDataSystem>
//     {
//         private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";
//         private Entity entity;
//         private IECSToCRDTWriter ecsToCRDTWriter;
//         private PlayerCRDTEntity playerCRDTEntity;
//         private Profile playerProfile;
//
//         private Avatar CreateTestAvatar() =>
//             new (BodyShape.MALE,
//                 WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
//                 WearablesConstants.DefaultColors.GetRandomEyesColor(),
//                 WearablesConstants.DefaultColors.GetRandomHairColor(),
//                 WearablesConstants.DefaultColors.GetRandomSkinColor());
//
//         [SetUp]
//         public void Setup()
//         {
//             ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
//
//             IComponentPool<PBAvatarEquippedData> componentPoolRegistry = Substitute.For<IComponentPool<PBAvatarEquippedData>>();
//             var instantiatedPbComponent = new PBAvatarEquippedData();
//             componentPoolRegistry.Get().Returns(instantiatedPbComponent);
//             system = new WriteAvatarEquippedDataSystem(world, ecsToCRDTWriter);
//
//             var wearableURNs = new List<URN>();
//             wearableURNs.Add("wearable-urn-1");
//             wearableURNs.Add("wearable-urn-2");
//             wearableURNs.Add("wearable-urn-3");
//             var emoteURNs = new List<URN>();
//             emoteURNs.Add("emote-urn-1");
//             emoteURNs.Add("emote-urn-2");
//             emoteURNs.Add("emote-urn-3");
//
//             playerProfileData = new Profile
//             {
//                 CRDTEntity = 3,
//                 Name = "CthulhuFhtaghn",
//                 WearableUrns = wearableURNs,
//                 EmoteUrns = emoteURNs,
//             };
//
//             Avatar avatar = CreateTestAvatar();
//             avatar.Wearables.
//             playerProfile = new Profile(FAKE_USER_ID, "fake user", avatar);
//
//             entity = world.Create(playerProfileData);
//         }
//
//         [TearDown]
//         public void TearDown()
//         {
//             world.Dispose();
//         }
//
//         [Test]
//         public void PropagateComponentCreationCorrectly()
//         {
//             Assert.IsFalse(world.Has<PBAvatarEquippedData>(entity));
//             Assert.IsFalse(world.Has<CRDTEntity>(entity));
//
//             system.Update(0);
//
//             ecsToCRDTWriter.Received(1)
//                            .PutMessage(
//                                 Arg.Any<Action<PBAvatarEquippedData, PBAvatarEquippedData>>(),
//                                 Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerProfileData.CRDTEntity.Id),
//                                 Arg.Is<PBAvatarEquippedData>(comp =>
//                                     comp.WearableUrns.Count == playerProfileData.WearableUrns.Count
//                                     && comp.WearableUrns[0] == playerProfileData.WearableUrns.First()
//                                     && comp.EmoteUrns[0] == playerProfileData.EmoteUrns.First()));
//
//             AssertPBComponentMatchesPlayerSDKData();
//         }
//
//         [Test]
//         public void PropagateComponentUpdateCorrectly()
//         {
//             Assert.IsFalse(world.Has<PBAvatarEquippedData>(entity));
//             Assert.IsFalse(world.Has<CRDTEntity>(entity));
//
//             system.Update(0);
//
//             ecsToCRDTWriter.Received(1)
//                            .PutMessage(
//                                 Arg.Any<Action<PBAvatarEquippedData, PBAvatarEquippedData>>(),
//                                 Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerProfileData.CRDTEntity.Id),
//                                 Arg.Is<PBAvatarEquippedData>(comp =>
//                                     comp.WearableUrns.Count == playerProfileData.WearableUrns.Count
//                                     && comp.WearableUrns[0] == playerProfileData.WearableUrns.First()
//                                     && comp.EmoteUrns[0] == playerProfileData.EmoteUrns.First()));
//
//             AssertPBComponentMatchesPlayerSDKData();
//
//             Assert.IsTrue(world.TryGet(entity, out playerProfileData));
//
//             playerProfileData.IsDirty = true;
//             playerProfileData.Name = "D460N";
//             playerProfileData.BodyShapeURN = "old:ones:02";
//             var newWearableURNs = new List<URN>();
//             newWearableURNs.Add("wearable-urn-4");
//             newWearableURNs.Add("wearable-urn-5");
//             newWearableURNs.Add("wearable-urn-6");
//             playerProfileData.WearableUrns = newWearableURNs;
//             var newEmoteURNs = new List<URN>();
//             newEmoteURNs.Add("emote-urn-4");
//             newEmoteURNs.Add("emote-urn-5");
//             newEmoteURNs.Add("emote-urn-6");
//             playerProfileData.EmoteUrns = newEmoteURNs;
//
//             world.Set(entity, playerProfileData);
//
//             system.Update(0);
//
//             Assert.IsTrue(world.TryGet(entity, out playerProfileData));
//
//             AssertPBComponentMatchesPlayerSDKData();
//         }
//
//         [Test]
//         public void HandleComponentRemovalCorrectly()
//         {
//             Assert.IsFalse(world.Has<PBAvatarEquippedData>(entity));
//
//             system.Update(0);
//
//             Assert.IsTrue(world.Has<PBAvatarEquippedData>(entity));
//
//             world.Remove<PlayerProfileDataComponent>(entity);
//
//             system.Update(0);
//
//             ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarEquippedData>(playerProfileData.CRDTEntity.Id);
//             Assert.IsFalse(world.Has<PBAvatarEquippedData>(entity));
//             Assert.IsFalse(world.Has<CRDTEntity>(entity));
//         }
//
//         private void AssertPBComponentMatchesPlayerSDKData()
//         {
//             Assert.IsTrue(world.TryGet(entity, out PBAvatarEquippedData pbComponent));
//
//             Assert.AreEqual(playerProfileData.WearableUrns.Count, pbComponent.WearableUrns.Count);
//
//             foreach (string urn in pbComponent.WearableUrns) { Assert.IsTrue(playerProfileData.WearableUrns.Contains(urn)); }
//
//             Assert.AreEqual(playerProfileData.EmoteUrns.Count, pbComponent.EmoteUrns.Count);
//
//             foreach (string urn in pbComponent.EmoteUrns) { Assert.IsTrue(playerProfileData.EmoteUrns.Contains(urn)); }
//
//             Assert.IsTrue(world.Has<CRDTEntity>(entity));
//         }
//     }
// }

