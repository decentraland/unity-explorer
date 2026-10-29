using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.ECSComponents;
using DCL.Ipfs;
using DCL.Profiles;
using Decentraland.Common;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.ColorComponent;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Entity = Arch.Core.Entity;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution,
    DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using QueryDescription = Arch.Core.QueryDescription;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarLoaderSystemShould : UnitySystemTestBase<AvatarLoaderSystem>
    {
        private const string PROFILE_FORCE_RENDER = "hat";

        private static readonly string BODY_SHAPE_MALE = BodyShape.MALE;
        private static readonly string BODY_SHAPE_FEMALE = BodyShape.FEMALE;
        private static readonly string FAKE_NAME = "FAKE_NAME_1";
        private static readonly string FAKE_ID = "1";
        private static readonly List<string> FAKE_WEARABLES = new () { "WEARABLE_1", "WEARABLE_2" };
        private static readonly URN PROFILE_WEARABLE = new ("urn:decentraland:off-chain:base-avatars:blue_tshirt");
        private static readonly URN SCENE_WEARABLE = new ("urn:decentraland:off-chain:scene-wearable:scene-hat");
        private static readonly URN OTHER_SCENE_WEARABLE = new ("urn:decentraland:off-chain:scene-wearable:scene-kilt");
        private static readonly URN CATALOG_WEARABLE = new ("urn:decentraland:off-chain:base-avatars:red_hoodie");
        private static readonly Color PROFILE_SKIN_COLOR = new (0.9f, 0.7f, 0.6f);
        private static readonly Color PROFILE_HAIR_COLOR = new (0.2f, 0.1f, 0f);
        private static readonly Color PROFILE_EYES_COLOR = new (0.1f, 0.3f, 0.5f);
        private static readonly Color SCENE_SKIN_COLOR = new (0.3f, 0.2f, 0.1f);
        private static readonly Color SCENE_HAIR_COLOR = new (1f, 0f, 0f);
        private static readonly Color SCENE_EYES_COLOR = new (0f, 1f, 0f);
        private static readonly QueryDescription EMOTE_PROMISES_QUERY = new QueryDescription().WithAll<EmotePromise>();
        private static readonly QueryDescription WEARABLE_PROMISES_QUERY = new QueryDescription().WithAll<WearablePromise>();

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
        public void ReplaceMainPlayerProfileWearablesWithSceneWearables()
        {
            // Arrange
            var sceneOverride = new SceneAvatarShapeOverride(new List<URN> { SCENE_WEARABLE, CATALOG_WEARABLE }) { IsDirty = true };
            Entity entity = world.Create(new PlayerComponent(), CreateProfile(), PartitionComponent.TOP_PRIORITY, sceneOverride);

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_MALE), SCENE_WEARABLE, CATALOG_WEARABLE }, avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            CollectionAssert.IsEmpty(avatarShapeComponent.WearablePromise.LoadingIntention.HideWearablesResolution.ForceRender);
            Assert.IsFalse(world.Get<SceneAvatarShapeOverride>(entity).IsDirty);
        }

        [Test]
        public void RebuildMainPlayerAvatarWhenSceneOverrideChanges()
        {
            // Arrange
            Profile profile = CreateProfile();
            Entity entity = world.Create(new PlayerComponent(), profile, PartitionComponent.TOP_PRIORITY, new SceneAvatarShapeOverride(new List<URN> { SCENE_WEARABLE }));
            system.Update(0);

            Entity originalPromise = world.Get<AvatarShapeComponent>(entity).WearablePromise.Entity;
            int emotePromisesCount = world.CountEntities(in EMOTE_PROMISES_QUERY);

            ref SceneAvatarShapeOverride sceneOverride = ref world.Get<SceneAvatarShapeOverride>(entity);
            sceneOverride.Wearables.Clear();
            sceneOverride.Wearables.Add(OTHER_SCENE_WEARABLE);
            sceneOverride.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            Assert.That(world.IsAlive(originalPromise), Is.False);
            Assert.IsTrue(avatarShapeComponent.IsDirty);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_MALE), OTHER_SCENE_WEARABLE }, avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            Assert.IsFalse(world.Get<SceneAvatarShapeOverride>(entity).IsDirty);
            Assert.IsFalse(profile.IsDirty);
            CollectionAssert.AreEquivalent(new[] { PROFILE_WEARABLE }, profile.Avatar.Wearables);
            Assert.AreEqual(emotePromisesCount, world.CountEntities(in EMOTE_PROMISES_QUERY));
        }

        [Test]
        public void ApplySceneOverrideWhenProfileAndOverrideChangeTogether()
        {
            // Arrange
            Profile profile = CreateProfile();
            Entity entity = world.Create(new PlayerComponent(), profile, PartitionComponent.TOP_PRIORITY, new SceneAvatarShapeOverride(new List<URN>()));
            system.Update(0);

            ref SceneAvatarShapeOverride sceneOverride = ref world.Get<SceneAvatarShapeOverride>(entity);
            sceneOverride.Wearables.Add(SCENE_WEARABLE);
            sceneOverride.SkinColor = SCENE_SKIN_COLOR;
            sceneOverride.IsDirty = true;
            profile.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_MALE), SCENE_WEARABLE }, avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            Assert.AreEqual(SCENE_SKIN_COLOR, avatarShapeComponent.SkinColor);
            Assert.IsFalse(world.Get<SceneAvatarShapeOverride>(entity).IsDirty);
            Assert.IsFalse(profile.IsDirty);
        }

        [Test]
        public void KeepProfileWearablesWhenSceneWearablesAreEmpty()
        {
            // Arrange
            var sceneOverride = new SceneAvatarShapeOverride(new List<URN>()) { BodyShape = BodyShape.FEMALE, IsDirty = true };
            Entity entity = world.Create(new PlayerComponent(), CreateProfile(), PartitionComponent.TOP_PRIORITY, sceneOverride);

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_FEMALE), PROFILE_WEARABLE }, avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            CollectionAssert.AreEquivalent(new[] { PROFILE_FORCE_RENDER }, avatarShapeComponent.WearablePromise.LoadingIntention.HideWearablesResolution.ForceRender);
        }

        [Test]
        public void ApplySceneBodyShapeAndColorsButKeepProfileIdentity()
        {
            // Arrange
            Profile profile = CreateProfile();
            var sceneOverride = new SceneAvatarShapeOverride(new List<URN>())
            {
                BodyShape = BodyShape.FEMALE,
                SkinColor = SCENE_SKIN_COLOR,
                HairColor = SCENE_HAIR_COLOR,
                EyesColor = SCENE_EYES_COLOR,
                IsDirty = true,
            };

            Entity entity = world.Create(profile, PartitionComponent.TOP_PRIORITY, sceneOverride);

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            Assert.AreEqual(BodyShape.FEMALE, avatarShapeComponent.BodyShape);
            Assert.AreEqual(BodyShape.FEMALE, avatarShapeComponent.WearablePromise.LoadingIntention.BodyShape);
            Assert.AreEqual(SCENE_SKIN_COLOR, avatarShapeComponent.SkinColor);
            Assert.AreEqual(SCENE_HAIR_COLOR, avatarShapeComponent.HairColor);
            Assert.AreEqual(SCENE_EYES_COLOR, avatarShapeComponent.EyesColor);
            Assert.IsFalse(avatarShapeComponent.ShowOnlyWearables);
            Assert.AreEqual(profile.Name, avatarShapeComponent.Name);
            Assert.AreEqual(profile.UserId.Value, avatarShapeComponent.ID);
        }

        [Test]
        public void KeepProfileColorsTheSceneLeavesUnset()
        {
            // Arrange
            var sceneOverride = new SceneAvatarShapeOverride(new List<URN>()) { HairColor = SCENE_HAIR_COLOR, IsDirty = true };
            Entity entity = world.Create(CreateProfile(), PartitionComponent.TOP_PRIORITY, sceneOverride);

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            Assert.AreEqual(BodyShape.MALE, avatarShapeComponent.BodyShape);
            Assert.AreEqual(PROFILE_SKIN_COLOR, avatarShapeComponent.SkinColor);
            Assert.AreEqual(SCENE_HAIR_COLOR, avatarShapeComponent.HairColor);
            Assert.AreEqual(PROFILE_EYES_COLOR, avatarShapeComponent.EyesColor);
        }

        [Test]
        public void ShowOnlySceneWearablesLikeAnNpc()
        {
            // Arrange
            var sceneOverride = new SceneAvatarShapeOverride(new List<URN>()) { ShowOnlyWearables = true, IsDirty = true };
            Entity entity = world.Create(CreateProfile(), PartitionComponent.TOP_PRIORITY, sceneOverride);

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            Assert.IsTrue(avatarShapeComponent.ShowOnlyWearables);
            Assert.AreEqual(0f, avatarShapeComponent.SkinColor.a);
            Assert.AreEqual(0f, avatarShapeComponent.HairColor.a);
            Assert.AreEqual(0f, avatarShapeComponent.EyesColor.a);
        }

        [Test]
        public void ApplyExistingSceneOverrideWhenCreatingRemoteAvatar()
        {
            // Arrange
            var sceneOverride = new SceneAvatarShapeOverride(new List<URN> { SCENE_WEARABLE }) { IsDirty = true };
            Entity entity = world.Create(CreateProfile(), PartitionComponent.TOP_PRIORITY, sceneOverride);

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_MALE), SCENE_WEARABLE }, avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            Assert.IsFalse(world.Get<SceneAvatarShapeOverride>(entity).IsDirty);
        }

        [Test]
        public void ReapplySceneOverrideWhenRemoteProfileChanges()
        {
            // Arrange
            Profile profile = CreateProfile();
            var sceneOverride = new SceneAvatarShapeOverride(new List<URN> { SCENE_WEARABLE }) { BodyShape = BodyShape.FEMALE, SkinColor = SCENE_SKIN_COLOR };
            Entity entity = world.Create(profile, PartitionComponent.TOP_PRIORITY, sceneOverride);
            system.Update(0);

            Entity originalPromise = world.Get<AvatarShapeComponent>(entity).WearablePromise.Entity;
            profile.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            Assert.That(world.IsAlive(originalPromise), Is.False);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_FEMALE), SCENE_WEARABLE }, avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            Assert.AreEqual(BodyShape.FEMALE, avatarShapeComponent.BodyShape);
            Assert.AreEqual(SCENE_SKIN_COLOR, avatarShapeComponent.SkinColor);
            Assert.AreEqual(PROFILE_HAIR_COLOR, avatarShapeComponent.HairColor);
            Assert.IsFalse(profile.IsDirty);
            Assert.IsFalse(world.Get<SceneAvatarShapeOverride>(entity).IsDirty);
        }

        [Test]
        public void RebuildRemoteAvatarWhenSceneOverrideIsAdded()
        {
            // Arrange
            Profile profile = CreateProfile();
            Entity entity = world.Create(profile, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            Entity originalPromise = world.Get<AvatarShapeComponent>(entity).WearablePromise.Entity;
            int emotePromisesCount = world.CountEntities(in EMOTE_PROMISES_QUERY);

            world.Add(entity, new SceneAvatarShapeOverride(new List<URN> { SCENE_WEARABLE }) { IsDirty = true });

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            Assert.That(world.IsAlive(originalPromise), Is.False);
            Assert.IsTrue(avatarShapeComponent.IsDirty);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_MALE), SCENE_WEARABLE }, avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            Assert.IsFalse(world.Get<SceneAvatarShapeOverride>(entity).IsDirty);
            Assert.IsFalse(profile.IsDirty);
            Assert.AreEqual(emotePromisesCount, world.CountEntities(in EMOTE_PROMISES_QUERY));
        }

        [Test]
        public void RestoreRemoteProfileLookWhenSceneOverrideIsCleared()
        {
            // Arrange
            var sceneOverride = new SceneAvatarShapeOverride(new List<URN> { SCENE_WEARABLE })
            {
                BodyShape = BodyShape.FEMALE,
                SkinColor = SCENE_SKIN_COLOR,
                HairColor = SCENE_HAIR_COLOR,
                EyesColor = SCENE_EYES_COLOR,
                ShowOnlyWearables = true,
            };

            Entity entity = world.Create(CreateProfile(), PartitionComponent.TOP_PRIORITY, sceneOverride);
            system.Update(0);

            ref SceneAvatarShapeOverride sceneOverrideRef = ref world.Get<SceneAvatarShapeOverride>(entity);
            sceneOverrideRef.Clear();
            sceneOverrideRef.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            AvatarShapeComponent avatarShapeComponent = world.Get<AvatarShapeComponent>(entity);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_MALE), PROFILE_WEARABLE }, avatarShapeComponent.WearablePromise.LoadingIntention.Pointers);
            CollectionAssert.AreEquivalent(new[] { PROFILE_FORCE_RENDER }, avatarShapeComponent.WearablePromise.LoadingIntention.HideWearablesResolution.ForceRender);
            Assert.AreEqual(BodyShape.MALE, avatarShapeComponent.BodyShape);
            Assert.AreEqual(PROFILE_SKIN_COLOR, avatarShapeComponent.SkinColor);
            Assert.AreEqual(PROFILE_HAIR_COLOR, avatarShapeComponent.HairColor);
            Assert.AreEqual(PROFILE_EYES_COLOR, avatarShapeComponent.EyesColor);
            Assert.IsFalse(avatarShapeComponent.ShowOnlyWearables);
        }

        [Test]
        public void RequestProfileWearableDTOsWhenSceneWearablesReplaceThem()
        {
            // Arrange
            var sceneOverride = new SceneAvatarShapeOverride(new List<URN> { SCENE_WEARABLE }) { BodyShape = BodyShape.FEMALE, IsDirty = true };
            world.Create(CreateProfile(), PartitionComponent.TOP_PRIORITY, sceneOverride);

            // Act
            system.Update(0);

            // Assert
            List<WearablePromise> promises = GetStandaloneWearablePromises();
            Assert.AreEqual(1, promises.Count);
            Assert.IsTrue(promises[0].LoadingIntention.DtoOnly);
            CollectionAssert.AreEqual(new[] { new URN(BODY_SHAPE_MALE), PROFILE_WEARABLE }, promises[0].LoadingIntention.Pointers);
        }

        [Test]
        public void RequestNewProfileWearableDTOsWhenRemoteProfileChangesUnderSceneWearables()
        {
            // Arrange
            Entity entity = world.Create(CreateProfile(), PartitionComponent.TOP_PRIORITY, new SceneAvatarShapeOverride(new List<URN> { SCENE_WEARABLE }));
            system.Update(0);

            Profile changedProfile = new ProfileBuilder().WithUserId(FAKE_ID).WithBodyShape(BodyShape.MALE).WithWearables(new[] { CATALOG_WEARABLE }).Build();
            changedProfile.IsDirty = true;
            world.Set(entity, changedProfile);

            // Act
            system.Update(0);

            // Assert
            List<WearablePromise> promises = GetStandaloneWearablePromises();
            Assert.AreEqual(2, promises.Count);
            Assert.IsTrue(promises.Exists(promise => promise.LoadingIntention.Pointers.Contains(CATALOG_WEARABLE)));
        }

        [Test]
        public void NotRequestProfileWearableDTOsWithoutSceneWearables()
        {
            // Arrange
            var sceneOverride = new SceneAvatarShapeOverride(new List<URN>()) { BodyShape = BodyShape.FEMALE, IsDirty = true };
            world.Create(CreateProfile(), PartitionComponent.TOP_PRIORITY, sceneOverride);

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.IsEmpty(GetStandaloneWearablePromises());
        }

        [Test]
        public void ReleaseResolvedProfileWearableDTOsPromise()
        {
            // Arrange
            world.Create(CreateProfile(), PartitionComponent.TOP_PRIORITY, new SceneAvatarShapeOverride(new List<URN> { SCENE_WEARABLE }));
            system.Update(0);

            WearablePromise promise = GetStandaloneWearablePromises()[0];
            world.Add(promise.Entity, new StreamableLoadingResult<WearablesResolution>(new WearablesResolution(WearableComponentsUtils.WEARABLES_POOL.Get()!)));

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.IsEmpty(GetStandaloneWearablePromises());
            Assert.IsFalse(world.IsAlive(promise.Entity));
        }

        [Test]
        public void LoadMainPlayerEmotesForTheSceneBodyShape()
        {
            // Arrange
            Entity entity = world.Create(new PlayerComponent(), CreateProfile(), PartitionComponent.TOP_PRIORITY, new SceneAvatarShapeOverride(new List<URN>()));
            system.Update(0);

            ref SceneAvatarShapeOverride sceneOverride = ref world.Get<SceneAvatarShapeOverride>(entity);
            sceneOverride.BodyShape = BodyShape.FEMALE;
            sceneOverride.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(1, CountEmotePromises(BodyShape.MALE));
            Assert.AreEqual(1, CountEmotePromises(BodyShape.FEMALE));
        }

        [Test]
        public void NotReloadMainPlayerEmotesWhenTheSceneKeepsTheBodyShape()
        {
            // Arrange
            Entity entity = world.Create(new PlayerComponent(), CreateProfile(), PartitionComponent.TOP_PRIORITY, new SceneAvatarShapeOverride(new List<URN>()));
            system.Update(0);

            ref SceneAvatarShapeOverride sceneOverride = ref world.Get<SceneAvatarShapeOverride>(entity);
            sceneOverride.SkinColor = SCENE_SKIN_COLOR;
            sceneOverride.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(1, CountEmotePromises(BodyShape.MALE));
            Assert.AreEqual(0, CountEmotePromises(BodyShape.FEMALE));
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

        private int CountEmotePromises(BodyShape bodyShape)
        {
            var count = 0;

            world.Query(in EMOTE_PROMISES_QUERY, (ref EmotePromise promise) =>
            {
                if (promise.LoadingIntention.BodyShape.Equals(bodyShape))
                    count++;
            });

            return count;
        }

        private List<WearablePromise> GetStandaloneWearablePromises()
        {
            var promises = new List<WearablePromise>();
            world.Query(in WEARABLE_PROMISES_QUERY, (ref WearablePromise promise) => promises.Add(promise));
            return promises;
        }

        private static Profile CreateProfile() =>
            new ProfileBuilder()
               .WithUserId(FAKE_ID)
               .WithBodyShape(BodyShape.MALE)
               .WithWearables(new[] { PROFILE_WEARABLE })
               .WithForceRender(new[] { PROFILE_FORCE_RENDER })
               .WithColors((PROFILE_HAIR_COLOR, PROFILE_EYES_COLOR, PROFILE_SKIN_COLOR))
               .Build();
    }
}
