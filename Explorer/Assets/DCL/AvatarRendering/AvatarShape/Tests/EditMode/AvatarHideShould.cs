using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Tests
{
    public class AvatarHideShould
    {
        private readonly BodyShape TEST_BODY_SHAPE = BodyShape.MALE;
        private List<IWearable> mockWearables;
        private IWearable upperMockWearable;
        private IWearable upperSkinWearable;


        public void SetUp()
        {
            upperMockWearable = Substitute.For<IWearable>();
            upperMockWearable.GetCategory().Returns(WearablesConstants.Categories.UPPER_BODY);
            var expectedUpperWearableHide = new HashSet<string> { WearablesConstants.Categories.LOWER_BODY, WearablesConstants.Categories.HANDS, WearablesConstants.Categories.SKIN };

            upperMockWearable
               .When(x => x.GetHidingList(Arg.Any<string>(), Arg.Any<HashSet<string>>()))
               .Do(callInfo =>
                {
                    HashSet<string> result = callInfo.Arg<HashSet<string>>();

                    foreach (string item in expectedUpperWearableHide) { result.Add(item); }
                });

            upperSkinWearable = Substitute.For<IWearable>();
            upperSkinWearable.GetCategory().Returns(WearablesConstants.Categories.SKIN);
            var expectedSkinWearableHide = new HashSet<string> { WearablesConstants.Categories.LOWER_BODY, WearablesConstants.Categories.HANDS, WearablesConstants.Categories.UPPER_BODY };

            upperSkinWearable
               .When(x => x.GetHidingList(Arg.Any<string>(), Arg.Any<HashSet<string>>()))
               .Do(callInfo =>
                {
                    HashSet<string> result = callInfo.Arg<HashSet<string>>();

                    foreach (string item in expectedSkinWearableHide) { result.Add(item); }
                });
        }


        public void HideWearables()
        {
            mockWearables = new List<IWearable>() { upperMockWearable };

            var hidingList = new HashSet<string>();
            AvatarWearableHide.ComposeHiddenCategoriesOrdered(TEST_BODY_SHAPE, null, mockWearables, hidingList);

            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.LOWER_BODY));
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.HANDS));
            Assert.IsFalse(hidingList.Contains(WearablesConstants.Categories.UPPER_BODY));
        }


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


        public void HideBodyShape()
        {
            mockWearables = new List<IWearable>() { upperMockWearable };

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
