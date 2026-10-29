using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using Runtime.Wearables;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class SceneWearableDTOFactoryShould
    {
        private const string SCENE_ID = "bafkreisceneid";
        private const string CONTENT_BASE_URL = "http://127.0.0.1:8000/content/contents/";
        private const string HAT_PATH = "assets/wearables/cap_hat.glb";
        private const string HAT_HASH = "bafkreihathash";

        private ISceneData sceneData = null!;
        private SceneEntityDefinition definition = null!;
        private AssetBundleManifestVersion sceneManifest = null!;

        [SetUp]
        public void SetUp()
        {
            sceneManifest = AssetBundleManifestVersion.CreateManualManifest();

            definition = new SceneEntityDefinition(SCENE_ID, new SceneMetadata(), sceneManifest)
            {
                content = new[]
                {
                    new ContentDefinition { file = HAT_PATH, hash = HAT_HASH },
                    new ContentDefinition { file = "assets/wearables/cap_texture.png", hash = "bafkreitexturehash" },
                    new ContentDefinition { file = "assets/wearables/nested/other.png", hash = "bafkreinestedhash" },
                    new ContentDefinition { file = "assets/models/tree.glb", hash = "bafkreitreehash" },
                    new ContentDefinition { file = "main.js", hash = "bafkreimainhash" },
                },
            };

            ISceneContent sceneContent = Substitute.For<ISceneContent>();
            sceneContent.ContentBaseUrl.Returns(URLDomain.FromString(CONTENT_BASE_URL));
            sceneContent.TryGetHash(Arg.Any<string>(), out Arg.Any<string>())
                        .Returns(x =>
                         {
                             foreach (ContentDefinition file in definition.content)
                             {
                                 if (!string.Equals(file.file, (string)x[0], StringComparison.OrdinalIgnoreCase)) continue;

                                 x[1] = file.hash;
                                 return true;
                             }

                             x[1] = string.Empty;
                             return false;
                         });

            sceneData = Substitute.For<ISceneData>();
            sceneData.SceneShortInfo.Returns(new SceneShortInfo(Vector2Int.zero, SCENE_ID));
            sceneData.SceneEntityDefinition.Returns(definition);
            sceneData.SceneContent.Returns(sceneContent);
        }

        [TestCase("assets/wearables/hoodie_m_upper_body.glb", "upper_body")]
        [TestCase("assets/wearables/jeans_lower_body.glb", "lower_body")]
        [TestCase("cap_hat.glb", "hat")]
        [TestCase("assets/beard_facial_hair.glb", "facial_hair")]
        [TestCase("assets/crown_top_head.glb", "top_head")]
        [TestCase("assets/gloves_hands_wear.glb", "hands_wear")]
        [TestCase("Assets/Cap_HAT.glb", "hat")]
        [TestCase("Assets/Cap_HAT.GLB", "hat")]
        public void ResolveCategoryFromLongestSuffix(string path, string expectedCategory)
        {
            // Act
            bool resolved = SceneWearableDTOFactory.TryGetCategory(path, out string? category);

            // Assert
            Assert.IsTrue(resolved);
            Assert.AreEqual(expectedCategory, category);
        }

        [TestCase("assets/wearables/hoodie.glb")]
        [TestCase("assets/wearables/hoodiehat.glb")]
        [TestCase("assets/wearables/x_body.glb")]
        [TestCase("assets/x_eyes.glb")]
        [TestCase("assets/x_body_shape.glb")]
        [TestCase("assets/x_hat.png")]
        [TestCase("assets/x_hat/model.glb")]
        public void RejectUnsupportedCategory(string path)
        {
            // Act
            bool resolved = SceneWearableDTOFactory.TryGetCategory(path, out string? category);

            // Assert
            Assert.IsFalse(resolved);
            Assert.IsNull(category);
        }

        [Test]
        public void NotCreateWhenCategoryIsUnknown()
        {
            // Act
            bool created = TryCreate("assets/models/tree.glb", false, out _, out WearableDTO? dto);

            // Assert
            Assert.IsFalse(created);
            Assert.IsNull(dto);
        }

        [Test]
        public void NotCreateWhenFileIsMissing()
        {
            // Act
            bool created = TryCreate("assets/wearables/missing_hat.glb", false, out _, out WearableDTO? dto);

            // Assert
            Assert.IsFalse(created);
            Assert.IsNull(dto);
        }

        [Test]
        public void NotCreateDeployedWhenManifestIsMissing()
        {
            // Arrange
            definition.assetBundleManifestVersion = null;

            // Act
            bool created = TryCreate(HAT_PATH, false, out _, out WearableDTO? dto);

            // Assert
            Assert.IsFalse(created);
            Assert.IsNull(dto);
        }

        [Test]
        public void NotCreateDeployedWhenManifestFailed()
        {
            // Arrange
            definition.assetBundleManifestVersion = AssetBundleManifestVersion.FAILED;

            // Act
            bool created = TryCreate(HAT_PATH, false, out _, out WearableDTO? dto);

            // Assert
            Assert.IsFalse(created);
            Assert.IsNull(dto);
        }

        [Test]
        public void CreateDeployedSceneWearable()
        {
            // Act
            bool created = TryCreate(HAT_PATH, false, out URN urn, out WearableDTO? dto);

            // Assert
            Assert.IsTrue(created);
            Assert.AreEqual($"{SceneWearableDTOFactory.SCENE_WEARABLE_PREFIX}:{SCENE_ID}-{HAT_HASH}", urn.ToString());
            Assert.AreEqual(5, urn.ToString().Split(':').Length);
            Assert.AreEqual(urn, urn.Shorten());

            Assert.AreEqual(SCENE_ID, dto!.id);
            Assert.AreSame(sceneManifest, dto.assetBundleManifestVersion);
            Assert.IsTrue(string.IsNullOrEmpty(dto.ContentDownloadUrl));
            Assert.AreEqual(urn.ToString(), dto.metadata.id);
            Assert.AreEqual("hat", dto.metadata.data.category);
            Assert.IsEmpty(dto.metadata.data.hides);
            Assert.IsEmpty(dto.metadata.data.replaces);
            CollectionAssert.AreEqual(new[] { WearableCategories.Categories.HANDS }, dto.metadata.data.removesDefaultHiding);
            Assert.IsEmpty(dto.metadata.data.tags);
            Assert.AreEqual(string.Empty, dto.metadata.thumbnail);

            AvatarAttachmentDTO.Representation representation = dto.metadata.data.representations[0];
            Assert.AreEqual(1, dto.metadata.data.representations.Length);
            CollectionAssert.AreEquivalent(new[] { BodyShape.MALE.Value, BodyShape.FEMALE.Value }, representation.bodyShapes);
            Assert.AreEqual(HAT_PATH, representation.mainFile);
            CollectionAssert.AreEqual(new[] { HAT_PATH }, representation.contents);

            CollectionAssert.AreEqual(new[] { HAT_PATH, "assets/wearables/cap_texture.png" }, Array.ConvertAll(dto.content, c => c.file));
            Assert.AreEqual(HAT_HASH, dto.content[0].hash);
        }

        [Test]
        public void CreateLocalSceneWearableWithoutManifest()
        {
            // Arrange
            definition.assetBundleManifestVersion = null;

            // Act
            bool created = TryCreate(HAT_PATH, true, out URN urn, out WearableDTO? dto);

            // Assert
            Assert.IsTrue(created);
            Assert.AreEqual($"{SceneWearableDTOFactory.SCENE_WEARABLE_PREFIX}:{SCENE_ID}-{HAT_HASH}", urn.ToString());
            Assert.AreEqual(CONTENT_BASE_URL, dto!.ContentDownloadUrl);
        }

        [Test]
        public void ResolveAsRegularUnisexWearable()
        {
            // Arrange
            TryCreate(HAT_PATH, false, out URN urn, out WearableDTO? dto);

            // Act
            IWearable wearable = new Wearable(new StreamableLoadingResult<WearableDTO>(dto!));
            IAvatarAttachment attachment = wearable;

            // Assert
            Assert.AreEqual(WearableType.Regular, wearable.Type);
            Assert.AreEqual(urn, wearable.GetUrn());
            Assert.IsTrue(attachment.IsUnisex());
            Assert.IsTrue(wearable.HasSameModelsForAllGenders());
            Assert.IsTrue(attachment.TryGetMainFileHash(BodyShape.FEMALE, out string? hash));
            Assert.AreEqual(HAT_HASH, hash);
            Assert.AreEqual("base", attachment.GetRarity());
            Assert.AreEqual("cap_hat.glb", attachment.GetName());
            Assert.DoesNotThrow(() => attachment.GetThumbnail());
        }

        private bool TryCreate(string path, bool local, out URN urn, out WearableDTO? dto)
        {
            dto = SceneWearableDTOFactory.TryGetUrn(sceneData, path, local, out urn, out string? category, out string? hash)
                ? SceneWearableDTOFactory.Create(sceneData, path, urn, category, hash, local)
                : null;

            return dto != null;
        }
    }
}
