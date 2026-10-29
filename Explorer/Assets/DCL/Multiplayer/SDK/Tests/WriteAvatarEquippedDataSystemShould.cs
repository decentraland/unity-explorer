using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using WriteAvatarEquippedDataSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteAvatarEquippedDataSystem;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WriteAvatarEquippedDataSystemShould : UnitySystemTestBase<WriteAvatarEquippedDataSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";
        private const string HELMET_URN = "urn:decentraland:off-chain:base-avatars:fake_helmet";
        private const string HAIR_URN = "urn:decentraland:off-chain:base-avatars:fake_hair";
        private const string HAT_URN = "urn:decentraland:off-chain:base-avatars:fake_hat";
        private const string OTHER_HAT_URN = "urn:decentraland:off-chain:base-avatars:fake_other_hat";

        private readonly List<PBAvatarEquippedData> writtenMessages = new ();

        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter = null!;
        private WearableStorage wearableStorage = null!;
        private SDKProfile profile = null!;
        private PlayerSceneCRDTEntity playerCRDTEntity;

        [OneTimeSetUp]
        public void OneTimeSetUp() =>
            EcsTestsUtils.SetUpFeaturesRegistry();

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void Setup()
        {
            writtenMessages.Clear();
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            ecsToCRDTWriter.PutMessage(
                                Arg.Any<Action<PBAvatarEquippedData, (SDKProfile, List<bool>?)>>(),
                                Arg.Any<CRDTEntity>(),
                                Arg.Any<(SDKProfile, List<bool>?)>())
                           .Returns(info =>
                            {
                                var message = new PBAvatarEquippedData();

                                info.ArgAt<Action<PBAvatarEquippedData, (SDKProfile, List<bool>?)>>(0)
                                    .Invoke(message, info.ArgAt<(SDKProfile, List<bool>?)>(2));

                                writtenMessages.Add(message);
                                return message;
                            });

            wearableStorage = new WearableStorage();

            system = new WriteAvatarEquippedDataSystem(world, ecsToCRDTWriter, wearableStorage);

            profile = new SDKProfile();
            OverrideProfile(Array.Empty<string>(), HELMET_URN, HAIR_URN);

            playerCRDTEntity = new PlayerSceneCRDTEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, Entity.Null);

            entity = world.Create(playerCRDTEntity);
        }

        protected override void OnTearDown()
        {
            world.Dispose();
        }

        [Test]
        public void DispatchAvatarEquippedDataUpdateCorrectly()
        {
            // Arrange
            StoreWearable(HELMET_URN, WearableCategories.Categories.HELMET);
            StoreWearable(HAIR_URN, WearableCategories.Categories.HAIR);
            world.Add(entity, profile);
            system.Update(0);
            profile.IsDirty = false;

            // Act
            profile.Name = "newName";
            profile.IsDirty = true;
            system.Update(0);

            // Assert
            Assert.AreEqual(2, writtenMessages.Count);
            CollectionAssert.AreEqual(new[] { HELMET_URN, HAIR_URN }, writtenMessages[1].WearableUrns);
        }

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            // Arrange
            world.Add(entity, profile);
            system.Update(0);

            // Act
            world.Add<DeleteEntityIntention>(entity);
            system.Update(0);

            // Assert
            ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarEquippedData>(playerCRDTEntity.CRDTEntity);
        }

        [Test]
        public void HideWearableHiddenByAnotherProfileWearable()
        {
            // Arrange
            StoreWearable(HELMET_URN, WearableCategories.Categories.HELMET, WearableCategories.Categories.HAIR);
            StoreWearable(HAIR_URN, WearableCategories.Categories.HAIR);
            world.Add(entity, profile);

            // Act
            system.Update(0);

            // Assert
            PBAvatarEquippedData message = writtenMessages[^1];
            AssertWearable(message, 0, HELMET_URN, true);
            AssertWearable(message, 1, HAIR_URN, false);
        }

        [Test]
        public void KeepForceRenderedWearableVisible()
        {
            // Arrange
            OverrideProfile(new[] { WearableCategories.Categories.HAIR }, HELMET_URN, HAIR_URN);
            StoreWearable(HELMET_URN, WearableCategories.Categories.HELMET, WearableCategories.Categories.HAIR);
            StoreWearable(HAIR_URN, WearableCategories.Categories.HAIR);
            world.Add(entity, profile);

            // Act
            system.Update(0);

            // Assert
            PBAvatarEquippedData message = writtenMessages[^1];
            AssertWearable(message, 0, HELMET_URN, true);
            AssertWearable(message, 1, HAIR_URN, true);
        }

        [Test]
        public void HideEarlierWearableOfTheSameCategory()
        {
            // Arrange
            OverrideProfile(Array.Empty<string>(), HAT_URN, OTHER_HAT_URN);
            StoreWearable(HAT_URN, WearableCategories.Categories.HAT);
            StoreWearable(OTHER_HAT_URN, WearableCategories.Categories.HAT);
            world.Add(entity, profile);

            // Act
            system.Update(0);

            // Assert
            PBAvatarEquippedData message = writtenMessages[^1];
            AssertWearable(message, 0, HAT_URN, false);
            AssertWearable(message, 1, OTHER_HAT_URN, true);
        }

        [Test]
        public void LeaveVisibilityUnsetUntilWearablesLoad()
        {
            // Arrange
            StoreWearable(HELMET_URN, WearableCategories.Categories.HELMET, WearableCategories.Categories.HAIR);
            wearableStorage.Set(new URN(HAIR_URN), IWearable.NewEmpty());
            world.Add(entity, profile);
            system.Update(0);
            profile.IsDirty = false;

            PBAvatarEquippedData unresolved = writtenMessages[^1];
            system.Update(0);
            int writesWhileLoading = writtenMessages.Count;

            // Act
            StoreWearable(HAIR_URN, WearableCategories.Categories.HAIR);
            system.Update(0);
            system.Update(0);

            // Assert
            Assert.AreEqual(2, unresolved.Wearables.Count);
            Assert.IsFalse(unresolved.Wearables[0].HasVisible);
            Assert.IsFalse(unresolved.Wearables[1].HasVisible);
            Assert.AreEqual(1, writesWhileLoading);

            Assert.AreEqual(2, writtenMessages.Count);
            AssertWearable(writtenMessages[1], 0, HELMET_URN, true);
            AssertWearable(writtenMessages[1], 1, HAIR_URN, false);
        }

        [Test]
        public void IgnoreSceneAvatarShapeOverride()
        {
            // Arrange
            StoreWearable(HELMET_URN, WearableCategories.Categories.HELMET);
            StoreWearable(HAIR_URN, WearableCategories.Categories.HAIR);
            StoreWearable(HAT_URN, WearableCategories.Categories.HAT, WearableCategories.Categories.HELMET, WearableCategories.Categories.HAIR);

            var sceneAvatarShape = new PBAvatarShape { Wearables = { HAT_URN }, IsDirty = true };
            world.Add(entity, profile);
            world.Add(entity, sceneAvatarShape);
            system.Update(0);
            profile.IsDirty = false;

            // Act
            sceneAvatarShape.IsDirty = true;
            system.Update(0);

            // Assert
            Assert.AreEqual(1, writtenMessages.Count);
            CollectionAssert.AreEqual(new[] { HELMET_URN, HAIR_URN }, writtenMessages[0].WearableUrns);
            AssertWearable(writtenMessages[0], 0, HELMET_URN, true);
            AssertWearable(writtenMessages[0], 1, HAIR_URN, true);
        }

        private void OverrideProfile(string[] forceRender, params string[] wearables)
        {
            var wearableUrns = new HashSet<URN>();

            foreach (string wearable in wearables)
                wearableUrns.Add(new URN(wearable));

            var avatar = new Avatar(BodyShape.MALE, wearableUrns,
                WearablesConstants.DefaultColors.GetRandomEyesColor(),
                WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor());
            avatar.forceRender.UnionWith(forceRender);

            profile.OverrideWith(new Profile(UserId.New(FAKE_USER_ID).Unwrap(), "fake user", avatar));
        }

        private void StoreWearable(string urn, string category, params string[] hides)
        {
            var dto = new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = urn,
                    data = new WearableDTO.WearableMetadataDto.DataDto
                    {
                        category = category,
                        hides = hides,
                        replaces = Array.Empty<string>(),
                        removesDefaultHiding = Array.Empty<string>(),
                        tags = Array.Empty<string>(),
                        representations = new[]
                        {
                            new AvatarAttachmentDTO.Representation
                            {
                                bodyShapes = new[] { BodyShape.MALE.Value },
                                overrideHides = Array.Empty<string>(),
                                overrideReplaces = Array.Empty<string>(),
                            },
                        },
                    },
                },
            };

            wearableStorage.Set(new URN(urn), new Wearable(new StreamableLoadingResult<WearableDTO>(dto)));
        }

        private static void AssertWearable(PBAvatarEquippedData message, int index, string urn, bool visible)
        {
            PBAvatarEquippedData.Types.EquippedWearable wearable = message.Wearables[index];

            Assert.AreEqual(urn, wearable.Urn);
            Assert.IsTrue(wearable.HasVisible, $"{urn} visibility is unset");
            Assert.AreEqual(visible, wearable.Visible, urn);
        }
    }
}
