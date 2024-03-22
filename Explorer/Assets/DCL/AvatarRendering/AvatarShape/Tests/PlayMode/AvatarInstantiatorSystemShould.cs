using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Utilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Emotes.Components;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.Components.EmotesResolution, DCL.AvatarRendering.Emotes.Components.GetEmotesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarInstantiatorSystemShould : UnitySystemTestBase<AvatarInstantiatorSystem>
    {
        private Entity avatarEntity;
        private AvatarShapeComponent avatarShapeComponent;

        private Color randomSkinColor;
        private Color randomHairColor;
        private Mesh avatarMesh;

        [SetUp]
        public async void Setup()
        {
            IReleasablePerformanceBudget budget = Substitute.For<IReleasablePerformanceBudget>();
            budget.TrySpendBudget().Returns(true);

            GameObject avatarBaseGameObject = await Addressables.LoadAssetAsync<GameObject>("AvatarBase_TestAsset");
            AvatarBase instantiatedAvatarBase = Object.Instantiate(avatarBaseGameObject.GetComponent<AvatarBase>());
            IComponentPool<AvatarBase> avatarPoolRegistry = Substitute.For<IComponentPool<AvatarBase>>();
            avatarPoolRegistry.Get().Returns(instantiatedAvatarBase);

            avatarMesh = await Addressables.LoadAssetAsync<Mesh>("Avatar_Male_Mesh_TestAsset");

            randomSkinColor = new Color(0.5f, 0.5f, 0.5f, 1);
            randomHairColor = new Color(0.75f, 0.75f, 0.75f, 1);

            UnityEngine.ComputeShader computeShader = await Addressables.LoadAssetAsync<UnityEngine.ComputeShader>("ComputeShaderSkinning_TestAsset");

            IObjectPool<UnityEngine.ComputeShader> computeShaderPool = Substitute.For<IObjectPool<UnityEngine.ComputeShader>>();
            computeShaderPool.Get().Returns(Object.Instantiate(computeShader));

            var partitionComponent = new PartitionComponent();

            var wearablePromise = WearablePromise.Create(world,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(BodyShape.MALE, new List<string>
                    { "skin", "hair" }, Array.Empty<string>()),
                partitionComponent);

            var emotePromise = EmotePromise.Create(world, new GetEmotesByPointersIntention(new URN[] { "clap" }, BodyShape.MALE), partitionComponent);

            world.Add(wearablePromise.Entity, new StreamableLoadingResult<WearablesResolution>(new WearablesResolution(new List<IWearable>
            {
                GetMockWearable("body_shape", WearablesConstants.Categories.BODY_SHAPE),
                GetMockWearable("skin", WearablesConstants.Categories.UPPER_BODY),
                GetMockWearable("hair", WearablesConstants.Categories.HAIR),
            })));

            avatarShapeComponent = new AvatarShapeComponent("TEST_AVATAR", "TEST_ID", BodyShape.MALE, wearablePromise,
                randomSkinColor, randomHairColor, emotePromise);

            var celShadingMaterial = await Addressables.LoadAssetAsync<Material>("Avatar_Toon_TestAsset");
            var materialPool = Substitute.For<IExtendedObjectPool<Material>>();
            materialPool.Get().Returns(new Material(celShadingMaterial), new Material(celShadingMaterial), new Material(celShadingMaterial));

            int TEST_RESOLUTION = 256;
            Texture texture = new Texture2D(TEST_RESOLUTION, TEST_RESOLUTION, TextureArrayConstants.DEFAULT_BASEMAP_TEXTURE_FORMAT, false, false);
            TextureArrayContainerFactory.ARRAY_TYPES_COUNT = 3;
            var defaultTextures = new Dictionary<string, Texture>
            {
                {
                    $"Main_{TEST_RESOLUTION}", texture
                },
                {
                    $"Normal_{TEST_RESOLUTION}", texture
                },
                {
                    $"Emmisive_{TEST_RESOLUTION}", texture
                }
            };
            var poolMaterialSetup = new PoolMaterialSetup
            {
                Pool = materialPool, TextureArrayContainer = TextureArrayContainerFactory.Create(celShadingMaterial.shader, TEST_RESOLUTION,  defaultTextures)
            };

            var materialPoolHandler = Substitute.For<IAvatarMaterialPoolHandler>();
            materialPoolHandler.GetMaterialPool(Arg.Any<int>()).Returns(poolMaterialSetup);

            var defaultFaceFeaturesHandler = Substitute.For<IDefaultFaceFeaturesHandler>();
            defaultFaceFeaturesHandler.GetDefaultFacialFeaturesDictionary(Arg.Any<BodyShape>()).Returns(new Dictionary<string, Texture>
            {
                {
                    WearablesConstants.Categories.EYES, new Texture2D(1, 1)
                },
                {
                    WearablesConstants.Categories.MOUTH, new Texture2D(1, 1)
                },
                {
                    WearablesConstants.Categories.EYEBROWS, new Texture2D(1, 1)
                }
            });

            system = new AvatarInstantiatorSystem(world, budget, budget, avatarPoolRegistry, materialPoolHandler, computeShaderPool,
                Substitute.For<IWearableAssetsCache>(), new ComputeShaderSkinning(), new FixedComputeBufferHandler(10000, 4, 4),
                new ObjectProxy<AvatarBase>(), defaultFaceFeaturesHandler);
        }

        private IWearable GetMockWearable(string materialName, string category)
        {
            IWearable mockWearable = Substitute.For<IWearable>();

            var assetBundleData
                = new StreamableLoadingResult<WearableAsset>?[BodyShape.COUNT];

            //Creating a hierarchy
            var avatarGameObject = new GameObject();
            avatarGameObject.transform.SetParent(avatarGameObject.transform);

            //Creating a fake SMR
            SkinnedMeshRenderer skinnedMeshRenderer = avatarGameObject.AddComponent<SkinnedMeshRenderer>();
            skinnedMeshRenderer.sharedMesh = avatarMesh;
            skinnedMeshRenderer.bones = new Transform[ComputeShaderConstants.BONE_COUNT];
            skinnedMeshRenderer.sharedMesh.bindposes = new Matrix4x4[ComputeShaderConstants.BONE_COUNT];

            //Creating a fake standard material
            var fakeABMaterial = new Material(Shader.Find("DCL/Universal Render Pipeline/Lit"))
            {
                name = materialName,
            };

            skinnedMeshRenderer.material = fakeABMaterial;

            var rendererInfo = new WearableAsset.RendererInfo(skinnedMeshRenderer, fakeABMaterial);

            var wearableAsset = new WearableAsset(avatarGameObject, new List<WearableAsset.RendererInfo> { rendererInfo }, null);
            wearableAsset.AddReference();

            assetBundleData[BodyShape.MALE]
                = new StreamableLoadingResult<WearableAsset>(wearableAsset);

            mockWearable.WearableAssetResults.Returns(assetBundleData);
            mockWearable.GetCategory().Returns(category);

            return mockWearable;
        }

        [Test]
        public async Task InstantiateAvatar()
        {
            // For some reason SetUp is not awaited, probably a Unity's bug
            await UniTask.WaitUntil(() => system != null && avatarMesh != null);

            //Arrange
            avatarEntity = world.Create(avatarShapeComponent, PartitionComponent.TOP_PRIORITY, new CharacterTransform());

            //Act
            system.Update(0);

            //Assert
            Assert.IsFalse(world.Get<AvatarShapeComponent>(avatarEntity).IsDirty);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables.Count, 3);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables[1].Instance.GetComponent<MeshRenderer>().material.GetColor(ComputeShaderConstants._BaseColour_ShaderID), randomSkinColor);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables[2].Instance.GetComponent<MeshRenderer>().material.GetColor(ComputeShaderConstants._BaseColour_ShaderID), randomHairColor);
        }

        [Test]
        public async Task UpdateInstantiatedAvatar()
        {
            // Arrange
            await InstantiateAvatar();

            // Act
            var newPromise = WearablePromise.Create(world,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(BodyShape.MALE, new List<string>(), Array.Empty<string>()),
                new PartitionComponent());

            world.Add(newPromise.Entity, new StreamableLoadingResult<WearablesResolution>(new WearablesResolution(new List<IWearable> { GetMockWearable("body_shape", WearablesConstants.Categories.BODY_SHAPE) })));

            world.Get<AvatarShapeComponent>(avatarEntity).IsDirty = true;
            world.Get<AvatarShapeComponent>(avatarEntity).WearablePromise = newPromise;

            system.Update(0);

            foreach (CachedWearable wearable in world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables)
                wearable.OriginalAsset.AddReference();

            // Assert
            Assert.IsFalse(world.Get<AvatarShapeComponent>(avatarEntity).IsDirty);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables.Count, 1);
        }

        [Test]
        public async Task DestroyInstantiatedAvatar()
        {
            // For some reason SetUp is not awaited, probably a Unity's bug
            await UniTask.WaitUntil(() => system != null && avatarMesh != null);

            //Arrange
            Entity entity = world.Create(avatarShapeComponent, PartitionComponent.TOP_PRIORITY, new TransformComponent());
            system.Update(0);

            //Act
            world.Add<DeleteEntityIntention>(entity);
            system.Update(0);

            //Assert
            Assert.AreEqual(world.Get<AvatarShapeComponent>(entity).InstantiatedWearables.Count, 0);
        }
    }
}
