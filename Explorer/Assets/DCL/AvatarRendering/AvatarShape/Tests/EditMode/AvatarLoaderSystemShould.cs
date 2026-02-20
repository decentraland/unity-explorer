using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;
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

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarLoaderSystemShould : UnitySystemTestBase<AvatarLoaderSystem>
    {
        private static readonly string BODY_SHAPE_MALE = BodyShape.MALE;
        private static readonly string BODY_SHAPE_FEMALE = BodyShape.FEMALE;
        private static readonly string FAKE_NAME = "FAKE_NAME_1";
        private static readonly string FAKE_ID = "1";
        private static readonly List<string> FAKE_WEARABLES = new () { "WEARABLE_1", "WEARABLE_2" };

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
            IAvatarHighlightData highlightData = Substitute.For<IAvatarHighlightData>();

            ipfsRealm.EntitiesActiveEndpoint.Returns(URLDomain.FromString("/entities/active"));
            realmData.Ipfs.Returns(ipfsRealm);
            system = new AvatarLoaderSystem(world, highlightData);

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
    }
}
