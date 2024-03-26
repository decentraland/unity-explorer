using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
using Decentraland.Common;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.ColorComponent;
using NUnit.Framework;
using System.Collections.Generic;
using Entity = Arch.Core.Entity;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarLoaderSystemShould : UnitySystemTestBase<AvatarLoaderSystem>
    {
        private readonly string BODY_SHAPE_MALE = BodyShape.MALE;
        private readonly string BODY_SHAPE_FEMALE = BodyShape.FEMALE;
        private readonly string FAKE_NAME = "FAKE_NAME_1";
        private readonly string FAKE_ID = "1";
        private readonly List<string> FAKE_WEARABLES = new () { "WEARABLE_1", "WEARABLE_2" };
        private PBAvatarShape pbAvatarShape;
        private List<string> FAKE_POINTERS;

        private Color3 FAKE_HAIR_COLOR;
        private Color3 FAKE_SKIN_COLOR;
        private Color3 FAKE_EYE_COLOR;

        [SetUp]
        public void Setup()
        {
            FAKE_HAIR_COLOR = WearablesConstants.DefaultColors.GetRandomHairColor3();
            FAKE_SKIN_COLOR = WearablesConstants.DefaultColors.GetRandomSkinColor3();
            FAKE_EYE_COLOR = WearablesConstants.DefaultColors.GetRandomEyesColor3();

            pbAvatarShape = new PBAvatarShape
            {
                BodyShape = BODY_SHAPE_MALE,
                Name = FAKE_NAME,
                Id = FAKE_ID,
                Wearables = { FAKE_WEARABLES },
                SkinColor = FAKE_SKIN_COLOR,
                HairColor = FAKE_HAIR_COLOR,
                EyeColor = FAKE_EYE_COLOR
            };

            system = new AvatarLoaderSystem(world);
            FAKE_POINTERS = new List<string>();
            FAKE_POINTERS.Add(BODY_SHAPE_MALE);
            FAKE_POINTERS.AddRange(FAKE_WEARABLES);
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
            Assert.AreEqual(avatarShapeComponent.SkinColor, FAKE_SKIN_COLOR.ToUnityColor());
            Assert.AreEqual(avatarShapeComponent.HairColor, FAKE_HAIR_COLOR.ToUnityColor());
            Assert.AreEqual(avatarShapeComponent.WearablePromise.LoadingIntention.Pointers.ToArray(), FAKE_POINTERS);
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
            int originalPromiseVersion = avatarShapeComponent.WearablePromise.Entity.Entity.Id;

            pbAvatarShape.BodyShape = BODY_SHAPE_FEMALE;
            pbAvatarShape.IsDirty = true;

            system.Update(0);

            //Assert
            //Should be different ids, because the promise was cancelled and a new one was created
            Assert.AreNotEqual(avatarShapeComponent.WearablePromise.Entity.Entity.Id, originalPromiseVersion);
        }
    }
}
