using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using DCL.AvatarRendering.AvatarShape.Rendering.Avatar;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.ComponentsPooling;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.TestTools;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;


namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class AvatarInstantiatorSystemShould : UnitySystemTestBase<AvatarInstantiatorSystem>
    {
        private AvatarBase instantiatedAvatarBase;
        private AvatarShapeComponent avatarShapeComponent;
        private Entity avatarEntity;
        private Color randomSkinColor;
        private Color randomHairColor;

        private Shader shader;
        private UnityEngine.ComputeShader computeShaderAsset;

        [SetUp]
        public void Setup()
        {
            IConcurrentBudgetProvider budgetProvider = Substitute.For<IConcurrentBudgetProvider>();
            budgetProvider.TrySpendBudget().Returns(true);

            instantiatedAvatarBase = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AvatarBase>("Assets/DCL/AvatarRendering/AvatarShape/Assets/AvatarBase.prefab"));
            IComponentPool<AvatarBase> avatarPoolRegistry = Substitute.For<IComponentPool<AvatarBase>>();
            avatarPoolRegistry.Get().Returns(instantiatedAvatarBase);

            Promise promise = Promise.Create(world,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(BodyShape.MALE, new List<string>(){ "skin", "hair"}),
                new PartitionComponent());
            world.Add(promise.Entity, new StreamableLoadingResult<IWearable[]>(new []{
                GetMockWearable("body_shape", WearablesConstants.Categories.BODY_SHAPE),
                GetMockWearable("skin", WearablesConstants.Categories.UPPER_BODY),
                GetMockWearable("hair", WearablesConstants.Categories.HAIR)}));

            randomSkinColor = new Color(0.5f, 0.5f, 0.5f, 1);
            randomHairColor = new Color(0.75f, 0.75f, 0.75f, 1);

            avatarShapeComponent = new AvatarShapeComponent("TEST_AVATAR", "TEST_ID", BodyShape.MALE, promise,
                randomSkinColor, randomHairColor);

            computeShaderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.ComputeShader>("Assets/DCL/AvatarRendering/AvatarShape/Assets/ComputeShaderSkinning.compute");
            IObjectPool<UnityEngine.ComputeShader> computeShaderPool = Substitute.For<IObjectPool<UnityEngine.ComputeShader>>();
            computeShaderPool.Get().Returns(Object.Instantiate(computeShaderAsset));

            shader = Shader.Find("Custom/Avatar_CelShading");
            IObjectPool<Material> materialPool = Substitute.For<IObjectPool<Material>>();
            materialPool.Get().Returns(new Material(shader), new Material(shader), new Material(shader));

            system = new AvatarInstantiatorSystem(world, budgetProvider, avatarPoolRegistry, materialPool, computeShaderPool,
                new TextureArrayContainer(), Substitute.For<IWearableAssetsCache>(), new ComputeShaderSkinning(), new FixedComputeBufferHandler(10000, 4, 4));
        }

        private IWearable GetMockWearable(string materialName, string category)
        {
            IWearable mockWearable = Substitute.For<IWearable>();

            var assetBundleData
                = new StreamableLoadingResult<WearableAsset>?[BodyShape.COUNT];

            //Creating a hierarchy
            GameObject avatarGameObject = new GameObject();
            avatarGameObject.transform.SetParent(avatarGameObject.transform);

            //Creating a fake SMR and material
            SkinnedMeshRenderer skinnedMeshRenderer = avatarGameObject.AddComponent<SkinnedMeshRenderer>();
            Material fakeABMaterial = new Material(Shader.Find("Standard"));
            fakeABMaterial.name = materialName;

            skinnedMeshRenderer.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DCL/AvatarRendering/AvatarShape/Assets/Avatar_Male_Mesh.asset");
            skinnedMeshRenderer.material = fakeABMaterial;

            WearableAsset.RendererInfo rendererInfo = new WearableAsset.RendererInfo(skinnedMeshRenderer, fakeABMaterial);

            assetBundleData[BodyShape.MALE]
                = new StreamableLoadingResult<WearableAsset>(new WearableAsset(avatarGameObject,
                    new List<WearableAsset.RendererInfo>() { rendererInfo}));

            mockWearable.WearableAssets.Returns(assetBundleData);
            mockWearable.GetCategory().Returns(category);
            return mockWearable ;
        }

        [Test]
        [RequiresPlayMode]
        public void InstantiateAvatar()
        {
            //Arrange
            avatarEntity = world.Create(avatarShapeComponent, PartitionComponent.TOP_PRIORITY, new TransformComponent());

            //Act
            system.Update(0);

            //Assert
            Assert.IsFalse(world.Get<AvatarShapeComponent>(avatarEntity).IsDirty);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables.Count, 3);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables[1].Instance.GetComponent<MeshRenderer>().material.GetColor(ComputeShaderConstants._BaseColour_ShaderID), randomSkinColor);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables[2].Instance.GetComponent<MeshRenderer>().material.GetColor(ComputeShaderConstants._BaseColour_ShaderID), randomHairColor);
        }

        [Test]
        [RequiresPlayMode]
        public void UpdateInstantiatedAvatar()
        {
            //Arrange
            InstantiateAvatar();

            //Act
            Promise newPromise = Promise.Create(world,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(BodyShape.MALE, new List<string>()),
                new PartitionComponent());
            world.Add(newPromise.Entity, new StreamableLoadingResult<IWearable[]>(new []{GetMockWearable("body_shape", WearablesConstants.Categories.BODY_SHAPE)}));

            world.Get<AvatarShapeComponent>(avatarEntity).IsDirty = true;
            world.Get<AvatarShapeComponent>(avatarEntity).WearablePromise = newPromise;
            system.Update(0);

            //Assert
            Assert.IsFalse(world.Get<AvatarShapeComponent>(avatarEntity).IsDirty);
            Assert.AreEqual(world.Get<AvatarShapeComponent>(avatarEntity).InstantiatedWearables.Count, 1);
        }

        [Test]
        [RequiresPlayMode]
        public void DestroyInstantiatedAvatar()
        {
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
