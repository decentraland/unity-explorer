using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.Ipfs;
using DCL.Profiles;
using Decentraland.Common;
using ECS;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.ColorComponent;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using Entity = Arch.Core.Entity;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using QueryDescription = Arch.Core.QueryDescription;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarLoaderSystemShould : UnitySystemTestBase<AvatarLoaderSystem>
    {
        private static readonly string BODY_SHAPE_MALE = BodyShape.MALE;
        private static readonly string BODY_SHAPE_FEMALE = BodyShape.FEMALE;
        private static readonly string FAKE_NAME = "FAKE_NAME_1";
        private static readonly string FAKE_ID = "1";
        private static readonly List<string> FAKE_WEARABLES = new () { "WEARABLE_1", "WEARABLE_2" };
        private static readonly URN PROFILE_WEARABLE = new ("urn:decentraland:off-chain:base-avatars:blue_tshirt");
        private static readonly URN SCENE_WEARABLE = new ("urn:decentraland:off-chain:scene-wearable:scene-hat");
        private static readonly URN OTHER_SCENE_WEARABLE = new ("urn:decentraland:off-chain:scene-wearable:scene-kilt");
        private static readonly QueryDescription EMOTE_PROMISES_QUERY = new QueryDescription().WithAll<EmotePromise>();

        private PBAvatarShape pbAvatarShape;
        private List<URN> fakePointers;

        private Color3 fakeHairColor;
        private Color3 fakeSkinColor;
        private Color3 fakeEyeColor;

        [SetUp]
        public void Setup()
        {
            fakeHairColor = WearablesConstants.DefaultColors.GetRandomHairColor3();
            fakeSkinColor = WearablesConstants.DefaultColors.GetRandomSkinColor3();
            fakeEyeColor = WearablesConstants.DefaultColors.GetRandomEyesColor3();

            pbAvatarShape = new PBAvatarShape
            {
                BodyShape = BODY_SHAPE_MALE,
                Name = FAKE_NAME,
                Id = FAKE_ID,
                Wearables = { FAKE_WEARABLES },
                SkinColor = fakeSkinColor,
                HairColor = fakeHairColor,
                EyeColor = fakeEyeColor
            };

            IRealmData realmData = Substitute.For<IRealmData>();
            IIpfsRealm ipfsRealm = Substitute.For<IIpfsRealm>();

            ipfsRealm.EntitiesActiveEndpoint.Returns(URLDomain.FromString("/entities/active"));
            realmData.Ipfs.Returns(ipfsRealm);
            system = new AvatarLoaderSystem(world);

            fakePointers = new List<URN>();
            fakePointers.Add(BODY_SHAPE_MALE);

            foreach (URN urn in FAKE_WEARABLES)
                fakePointers.Add(urn);
        }

        [Test]
        public void StartAvatarLoad()
        {
            //Arrange
            Entity entity = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY);

            //Act
            system.Update(0);

            //Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            Assert.AreEqual(avatarShapeComponent.BodyShape.Value, BODY_SHAPE_MALE);
            Assert.AreEqual(avatarShapeComponent.Name, FAKE_NAME);
            Assert.AreEqual(avatarShapeComponent.ID, FAKE_ID);
            Assert.AreEqual(avatarShapeComponent.SkinColor, fakeSkinColor.ToUnityColor());
            Assert.AreEqual(avatarShapeComponent.HairColor, fakeHairColor.ToUnityColor());
            Assert.AreEqual(avatarShapeComponent.WearablePromise.LoadingIntention.Pointers.ToArray(), fakePointers);
        }

        [Test]
        public void UpdateAvatarLoad()
        {
            //Arrange
            Entity entity = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            ref AvatarShapeComponent avatarShapeComponent = ref world.Get<AvatarShapeComponent>(entity);
            Assert.AreEqual(avatarShapeComponent.BodyShape.Value, BODY_SHAPE_MALE);

            //Act
            pbAvatarShape.BodyShape = BODY_SHAPE_FEMALE;
            pbAvatarShape.IsDirty = true;
            system.Update(0);

            //Assert
            Assert.AreEqual(avatarShapeComponent.BodyShape.Value, BODY_SHAPE_FEMALE);
        }

        [Test]
        public void CancelAvatarLoad()
        {
            Entity entity = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            ref AvatarShapeComponent avatarShapeComponent = ref world.Get<AvatarShapeComponent>(entity);
            Entity originalPromise = avatarShapeComponent.WearablePromise.Entity;

            pbAvatarShape.BodyShape = BODY_SHAPE_FEMALE;
            pbAvatarShape.IsDirty = true;

            system.Update(0);

            //Assert
            //Should be different ids, because the promise was cancelled and a new one was created
            Assert.That(world.IsAlive(originalPromise), Is.False);
            Assert.AreNotEqual(avatarShapeComponent.WearablePromise.Entity, originalPromise);
        }

        [Test]
        public void AppendSceneWearablesAfterMainPlayerProfileWearables()
        {
            // Arrange
            var sceneWearables = new SceneWearablesOverride(new List<URN> { SCENE_WEARABLE }) { IsDirty = true };
            Entity entity = world.Create(new PlayerComponent(), CreateMainPlayerProfile(), PartitionComponent.TOP_PRIORITY, sceneWearables);

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_MALE), PROFILE_WEARABLE, SCENE_WEARABLE }, avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            Assert.IsFalse(world.Get<SceneWearablesOverride>(entity).IsDirty);
        }

        [Test]
        public void RebuildMainPlayerAvatarWhenSceneWearablesChange()
        {
            // Arrange
            Profile profile = CreateMainPlayerProfile();
            Entity entity = world.Create(new PlayerComponent(), profile, PartitionComponent.TOP_PRIORITY, new SceneWearablesOverride(new List<URN> { SCENE_WEARABLE }));
            system.Update(0);

            Entity originalPromise = world.Get<AvatarShapeComponent>(entity).WearablePromise.Entity;
            int emotePromisesCount = world.CountEntities(in EMOTE_PROMISES_QUERY);

            ref SceneWearablesOverride sceneWearables = ref world.Get<SceneWearablesOverride>(entity);
            sceneWearables.Urns.Clear();
            sceneWearables.Urns.Add(OTHER_SCENE_WEARABLE);
            sceneWearables.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            Assert.That(world.IsAlive(originalPromise), Is.False);
            Assert.IsTrue(avatarShapeComponent.IsDirty);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_MALE), PROFILE_WEARABLE, OTHER_SCENE_WEARABLE }, avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            Assert.IsFalse(world.Get<SceneWearablesOverride>(entity).IsDirty);
            Assert.IsFalse(profile.IsDirty);
            CollectionAssert.AreEquivalent(new[] { PROFILE_WEARABLE }, profile.Avatar.Wearables);
            Assert.AreEqual(emotePromisesCount, world.CountEntities(in EMOTE_PROMISES_QUERY));
        }

        [Test]
        public void MergeSceneWearablesWhenProfileAndOverrideChangeTogether()
        {
            // Arrange
            Profile profile = CreateMainPlayerProfile();
            Entity entity = world.Create(new PlayerComponent(), profile, PartitionComponent.TOP_PRIORITY, new SceneWearablesOverride(new List<URN>()));
            system.Update(0);

            ref SceneWearablesOverride sceneWearables = ref world.Get<SceneWearablesOverride>(entity);
            sceneWearables.Urns.Add(SCENE_WEARABLE);
            sceneWearables.IsDirty = true;
            profile.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_MALE), PROFILE_WEARABLE, SCENE_WEARABLE }, avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            Assert.IsFalse(world.Get<SceneWearablesOverride>(entity).IsDirty);
            Assert.IsFalse(profile.IsDirty);
        }

        [Test]
        public void AddAvatarHighlightComponentWhenCreatingAvatarFromSDKComponent()
        {
            // Arrange
            Entity entity = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<AvatarHighlightComponent>(entity), Is.True);
            AvatarHighlightComponent highlightComponent = world.Get<AvatarHighlightComponent>(entity);
            Assert.That(highlightComponent.Opacity, Is.EqualTo(0));
        }

        [Test]
        public void AddAvatarHighlightComponentWhenCreatingAvatarFromProfile()
        {
            ProfileBuilder builder = new ProfileBuilder().WithUserId(FAKE_ID);
            // Arrange
            Entity entity = world.Create(builder.Build(), PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<AvatarHighlightComponent>(entity), Is.True);
            AvatarHighlightComponent highlightComponent = world.Get<AvatarHighlightComponent>(entity);
            Assert.That(highlightComponent.Opacity, Is.EqualTo(0));
        }

        private static Profile CreateMainPlayerProfile() =>
            new ProfileBuilder()
               .WithUserId(FAKE_ID)
               .WithBodyShape(BodyShape.MALE)
               .WithWearables(new[] { PROFILE_WEARABLE })
               .Build();
    }
}
