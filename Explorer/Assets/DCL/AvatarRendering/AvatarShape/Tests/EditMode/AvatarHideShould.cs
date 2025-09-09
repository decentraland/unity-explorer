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
            WearableComponentsUtils.ComposeHiddenCategoriesOrdered(TEST_BODY_SHAPE, null, mockWearables, hidingList);

            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.LOWER_BODY));
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.HANDS));
            Assert.IsFalse(hidingList.Contains(WearablesConstants.Categories.UPPER_BODY));
        }

        [Test]
        public void HideHierarchyRespected()
        {
            // Helmet hides head, eyewear and hair
            // Mask hides helmet and top head
            // Top head hides helmet and mask

            mockWearables = new List<IWearable>
            {
                // Helmet
                new FakeWearable(new WearableDTO
                {
                    metadata = new WearableDTO.WearableMetadataDto
                    {
                        data = new WearableDTO.WearableMetadataDto.DataDto
                        {
                            category = WearablesConstants.Categories.HELMET,
                        },
                    },
                }, new HashSet<string>
                {
                    WearablesConstants.Categories.HEAD,
                    WearablesConstants.Categories.EYEWEAR,
                    WearablesConstants.Categories.HAIR,
                }),
                // Mask
                new FakeWearable(new WearableDTO
                {
                    metadata = new WearableDTO.WearableMetadataDto
                    {
                        data = new WearableDTO.WearableMetadataDto.DataDto
                        {
                            category = WearablesConstants.Categories.MASK,
                        },
                    },
                }, new HashSet<string>
                {
                    WearablesConstants.Categories.HELMET,
                    WearablesConstants.Categories.TOP_HEAD,
                }),
                // Top head
                new FakeWearable(new WearableDTO
                {
                    metadata = new WearableDTO.WearableMetadataDto
                    {
                        data = new WearableDTO.WearableMetadataDto.DataDto
                        {
                            category = WearablesConstants.Categories.TOP_HEAD,
                        },
                    },
                }, new HashSet<string>
                {
                    WearablesConstants.Categories.HELMET,
                    WearablesConstants.Categories.MASK,
                }),
                // Eyewear
                new FakeWearable(new WearableDTO
                {
                    metadata = new WearableDTO.WearableMetadataDto
                    {
                        data = new WearableDTO.WearableMetadataDto.DataDto
                        {
                            category = WearablesConstants.Categories.EYEWEAR,
                        },
                    },
                }),
                // Hair
                new FakeWearable(new WearableDTO
                {
                    metadata = new WearableDTO.WearableMetadataDto
                    {
                        data = new WearableDTO.WearableMetadataDto.DataDto
                        {
                            category = WearablesConstants.Categories.HAIR,
                        },
                    },
                }),
            };

            var hidingList = new HashSet<string>();
            WearableComponentsUtils.ComposeHiddenCategoriesOrdered(TEST_BODY_SHAPE, null, mockWearables, hidingList);

            // So hiding list should be:
            // head (by helmet)
            // eyewear (by helmet)
            // hair (by helmet)
            // helmet (by top head)
            // mask (by top head)

            // Hidden by helmet and top head
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.HEAD));
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.EYEWEAR));
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.HAIR));
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.HELMET));
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.MASK));

            // Since mask is hidden by higher priority top head
            Assert.False(hidingList.Contains(WearablesConstants.Categories.TOP_HEAD));
        }

        [Test]
        public void ForceRenderRespected()
        {
            mockWearables = new List<IWearable>() { upperMockWearable };

            var forceRender = new HashSet<string>();
            forceRender.Add(WearablesConstants.Categories.LOWER_BODY);

            var hidingList = new HashSet<string>();
            WearableComponentsUtils.ComposeHiddenCategoriesOrdered(TEST_BODY_SHAPE, forceRender, mockWearables, hidingList);

            Assert.IsFalse(hidingList.Contains(WearablesConstants.Categories.LOWER_BODY));
            Assert.IsTrue(hidingList.Contains(WearablesConstants.Categories.HANDS));
            Assert.IsFalse(hidingList.Contains(WearablesConstants.Categories.UPPER_BODY));
        }

        [Test]
        public void HideBodyShape()
        {
            mockWearables = new List<IWearable> { upperMockWearable };

            var hidingList = new HashSet<string>();
            WearableComponentsUtils.ComposeHiddenCategoriesOrdered(TEST_BODY_SHAPE, null, mockWearables, hidingList);

            var usedCategories = new HashSet<string>();
            usedCategories.Add(WearablesConstants.Categories.HEAD);

            var fakeBodyShape = new GameObject();

            GameObject fakeUpper = AddFakeBodyPart(fakeBodyShape, "ubody");
            GameObject fakeLower = AddFakeBodyPart(fakeBodyShape, "lbody");
            GameObject fakeHands = AddFakeBodyPart(fakeBodyShape, "hands");
            GameObject fakeHead = AddFakeBodyPart(fakeBodyShape, "head");

            WearableComponentsUtils.HideBodyShape(fakeBodyShape, hidingList, usedCategories);

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
