﻿using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
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
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution, DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using IAvatarAttachment = DCL.AvatarRendering.Loading.Components.IAvatarAttachment;

namespace DCL.AvatarRendering.AvatarShape.Tests.Instantiate
{
    [Ignore("This test fails on the GameCI runner because it calls Unity with -nographics. Ignore until we figure out what to do.")]
    public class AvatarInstantiatorSystemShould : UnitySystemTestBase<AvatarInstantiatorSystem>
    {
        private Entity avatarEntity;
        private AvatarShapeComponent avatarShapeComponent;

        private Color randomSkinColor;
        private Color randomHairColor;
        private Color randomEyesColor;
        private Mesh avatarMesh;

        private async Task SetupAsync()
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
            randomEyesColor = new Color(0.25f, 0.25f, 0.25f, 1);

            UnityEngine.ComputeShader computeShader = await Addressables.LoadAssetAsync<UnityEngine.ComputeShader>("ComputeShaderSkinning_TestAsset");

            IObjectPool<UnityEngine.ComputeShader> computeShaderPool = Substitute.For<IObjectPool<UnityEngine.ComputeShader>>();
            computeShaderPool.Get().Returns(Object.Instantiate(computeShader));

            var partitionComponent = new PartitionComponent();

            var wearablePromise = WearablePromise.Create(world,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(BodyShape.MALE, new List<string>
                    { "skin", "hair" }, Array.Empty<string>()),
                partitionComponent);

            world.Add(wearablePromise.Entity, new StreamableLoadingResult<WearablesResolution>(new WearablesResolution(new List<IWearable>
            {
                GetMockWearable("body_shape", WearablesConstants.Categories.BODY_SHAPE),
                GetMockWearable("skin", WearablesConstants.Categories.UPPER_BODY),
                GetMockWearable("hair", WearablesConstants.Categories.HAIR),
            })));

            avatarShapeComponent = new AvatarShapeComponent("TEST_AVATAR", "TEST_ID", BodyShape.MALE, wearablePromise,
                randomSkinColor, randomHairColor, randomEyesColor);

            Material? celShadingMaterial = await Addressables.LoadAssetAsync<Material>("Avatar_Toon_TestAsset");
            IExtendedObjectPool<Material>? materialPool = Substitute.For<IExtendedObjectPool<Material>>();
            materialPool.Get().Returns(new Material(celShadingMaterial), new Material(celShadingMaterial), new Material(celShadingMaterial));

            var textureArrayContainer = AvatarInstantiatorAssetsShould.NewTextureArrayContainer(celShadingMaterial.shader);
            var poolMaterialSetup = new PoolMaterialSetup(materialPool, textureArrayContainer);

            IAvatarMaterialPoolHandler? materialPoolHandler = Substitute.For<IAvatarMaterialPoolHandler>();
            materialPoolHandler.GetMaterialPool(Arg.Any<int>()).Returns(poolMaterialSetup);

            IDefaultFaceFeaturesHandler? defaultFaceFeaturesHandler = Substitute.For<IDefaultFaceFeaturesHandler>();

            defaultFaceFeaturesHandler.GetDefaultFacialFeaturesDictionary(Arg.Any<BodyShape>())
                                      .Returns(new FacialFeaturesTextures(new Dictionary<string, Dictionary<int, Texture>>
                                       {
                                           [WearablesConstants.Categories.EYES] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = new Texture2D(1, 1) },
                                           [WearablesConstants.Categories.MOUTH] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = new Texture2D(1, 1) },
                                           [WearablesConstants.Categories.EYEBROWS] = new () { [WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE] = new Texture2D(1, 1) },
                                       }));

            system = new AvatarInstantiatorSystem(world, budget, budget, avatarPoolRegistry, materialPoolHandler, computeShaderPool,
                Substitute.For<IAttachmentsAssetsCache>(), new ComputeShaderSkinning(), new FixedComputeBufferHandler(10000, 4, 4),
                new ObjectProxy<AvatarBase>(), defaultFaceFeaturesHandler, new WearableStorage(),
                new AvatarTransformMatrixJobWrapper());
        }

        private IEmote GetMockEmote(string materialName, string category)
        {
            (IEmote mockWearable, AttachmentRegularAsset wearableAsset) = GetMockedAvatarAttachment<IEmote>(materialName, category);

            mockWearable.AssetResults.Returns(
                new StreamableLoadingResult<AttachmentRegularAsset>?[] { new StreamableLoadingResult<AttachmentRegularAsset>(wearableAsset) });

            return mockWearable;
        }

        private IWearable GetMockWearable(string materialName, string category)
        {
            (IWearable mockWearable, AttachmentRegularAsset wearableAsset) = GetMockedAvatarAttachment<IWearable>(materialName, category);

            mockWearable.WearableAssetResults.Returns(new WearableAssets[]
            {
                new StreamableLoadingResult<AttachmentAssetBase>(wearableAsset),
            });

            return mockWearable;
        }

        private (T, AttachmentRegularAsset) GetMockedAvatarAttachment<T>(string materialName, string category) where T: class, IAvatarAttachment
        {
            T mockWearable = Substitute.For<T>();

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

            var dto = new EmoteDTO();
            dto.metadata = new EmoteDTO.EmoteMetadataDto();
            dto.metadata.emoteDataADR74 = new EmoteDTO.EmoteMetadataDto.Data();
            dto.metadata.emoteDataADR74.category = category;

            mockWearable.DTO.Returns(dto);

            var rendererInfo = new AttachmentRegularAsset.RendererInfo(fakeABMaterial);

            var wearableAsset = new AttachmentRegularAsset(avatarGameObject, new List<AttachmentRegularAsset.RendererInfo> { rendererInfo }, null);
            wearableAsset.AddReference();

            return (mockWearable, wearableAsset);
        }

        [Test]
        public async Task InstantiateAvatar()
        {
            // For some reason SetUp is not awaited, probably a Unity's bug
            await SetupAsync();

            //Arrange
            avatarEntity = world.Create(avatarShapeComponent, PartitionComponent.TOP_PRIORITY, new CharacterTransform());

            //Act
            system.Update(0);

            //Assert
            Assert.IsFalse(world.Get<AvatarShapeComponent>(avatarEntity).IsDirty);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables.Count, 3);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables[1].Instance.GetComponent<MeshRenderer>().material.GetColor(ComputeShaderConstants.BASE_COLOUR_SHADER_ID), randomSkinColor);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables[2].Instance.GetComponent<MeshRenderer>().material.GetColor(ComputeShaderConstants.BASE_COLOUR_SHADER_ID), randomHairColor);
        }

        [Test]
        public async Task UpdateInstantiatedAvatar()
        {
            // Arrange
            await InstantiateAvatar();

            // Act
            var newWearablePromise = WearablePromise.Create(world,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(BodyShape.MALE, new List<string>(), Array.Empty<string>()),
                new PartitionComponent());

            world.Add(newWearablePromise.Entity, new StreamableLoadingResult<WearablesResolution>(new WearablesResolution(new List<IWearable> { GetMockWearable("body_shape", WearablesConstants.Categories.BODY_SHAPE) })));

            world.Get<AvatarShapeComponent>(avatarEntity).IsDirty = true;
            world.Get<AvatarShapeComponent>(avatarEntity).WearablePromise = newWearablePromise;

            system.Update(0);

            foreach (var wearable in world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables)
                wearable.OriginalAsset.AddReference();

            // Assert
            Assert.IsFalse(world.Get<AvatarShapeComponent>(avatarEntity).IsDirty);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables.Count, 1);
        }

        [Test]
        public async Task DestroyInstantiatedAvatar()
        {
            // For some reason SetUp is not awaited, probably a Unity's bug
            await SetupAsync();

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
