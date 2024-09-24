using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Tests.EditMode;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Tests
{
    public class AvatarHideShould
    {
        private static readonly BodyShape TEST_BODY_SHAPE = BodyShape.MALE;
        private List<IWearable> mockWearables;
        private IWearable upperMockWearable;
        private IWearable upperSkinWearable;

        [SetUp]
        public void SetUp()
        {
            var mockDto = new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    data = new WearableDTO.WearableMetadataDto.DataDto
                    {
                        category = WearablesConstants.Categories.UPPER_BODY,
                    },
                },
            };

            upperMockWearable = new FakeWearable(
                mockDto,
                new HashSet<string>
                {
                    WearablesConstants.Categories.LOWER_BODY,
                    WearablesConstants.Categories.HANDS,
                    WearablesConstants.Categories.SKIN
                }
            );

            var skinDto = new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    data = new WearableDTO.WearableMetadataDto.DataDto
                    {
                        category = WearablesConstants.Categories.SKIN,
                    },
                },
            };

            upperSkinWearable = new FakeWearable(
                skinDto,
                new HashSet<string>
                {
                    WearablesConstants.Categories.LOWER_BODY,
                    WearablesConstants.Categories.HANDS,
                    WearablesConstants.Categories.UPPER_BODY
                }
            );
        }

        [Test]
        public void HideWearables()
        {
            mockWearables = new List<IWearable> { upperMockWearable };

            var hidingList = new HashSet<string>();
            AvatarWearableHide.ComposeHiddenCategoriesOrdered(TEST_BODY_SHAPE, null, mockWearables, hidingList);

            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.LOWER_BODY));
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.HANDS));
            Assert.IsFalse(hidingList.Contains(WearablesConstants.Categories.UPPER_BODY));
        }

        [Test]
        public void HideHierarchyRespected()
        {
            mockWearables = new List<IWearable>() { upperMockWearable, upperSkinWearable };

            var hidingList = new HashSet<string>();
            AvatarWearableHide.ComposeHiddenCategoriesOrdered(TEST_BODY_SHAPE, null, mockWearables, hidingList);

            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.UPPER_BODY));
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.HANDS));
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.LOWER_BODY));
            Assert.IsFalse(hidingList.Contains(WearablesConstants.Categories.SKIN));
        }

        [Test]
        public void ForceRenderRespected()
        {
            mockWearables = new List<IWearable>() { upperMockWearable };

            var forceRender = new HashSet<string>();
            forceRender.Add(WearablesConstants.Categories.LOWER_BODY);

            var hidingList = new HashSet<string>();
            AvatarWearableHide.ComposeHiddenCategoriesOrdered(TEST_BODY_SHAPE, forceRender, mockWearables, hidingList);

            Assert.IsFalse(hidingList.Contains(WearablesConstants.Categories.LOWER_BODY));
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.HANDS));
            Assert.IsFalse(hidingList.Contains(WearablesConstants.Categories.UPPER_BODY));
        }

        [Test]
        public void HideBodyShape()
        {
            mockWearables = new List<IWearable> { upperMockWearable };

            var hidingList = new HashSet<string>();
            AvatarWearableHide.ComposeHiddenCategoriesOrdered(TEST_BODY_SHAPE, null, mockWearables, hidingList);

            var usedCategories = new HashSet<string>();
            usedCategories.Add(WearablesConstants.Categories.HEAD);

            var fakeBodyShape = new GameObject();

            GameObject fakeUpper = AddFakeBodyPart(fakeBodyShape, "ubody");
            GameObject fakeLower = AddFakeBodyPart(fakeBodyShape, "lbody");
            GameObject fakeHands = AddFakeBodyPart(fakeBodyShape, "hands");
            GameObject fakeHead = AddFakeBodyPart(fakeBodyShape, "head");

            AvatarWearableHide.HideBodyShape(fakeBodyShape, hidingList, usedCategories);

            Assert.IsTrue(fakeUpper.gameObject.activeSelf);
            Assert.IsFalse(fakeLower.gameObject.activeSelf);
            Assert.IsFalse(fakeHands.gameObject.activeSelf);
            Assert.IsFalse(fakeHead.gameObject.activeSelf);
        }

        private static GameObject AddFakeBodyPart(GameObject fakeBodyShape, string name)
        {
            var fakeBodyPart = new GameObject(name);
            fakeBodyPart.AddComponent<SkinnedMeshRenderer>();
            fakeBodyPart.transform.SetParent(fakeBodyShape.transform);
            return fakeBodyPart;
        }
    }
}
