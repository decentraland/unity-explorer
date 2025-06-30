using Arch.Core;
using CRDT;
using DCL.AssetsProvision;
using DCL.DemoWorlds;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.SDKComponents.VideoPlayer;
using ECS.Unity.Materials;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Pooling;
using ECS.Unity.Materials.Systems;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.SDKComponents.NFTShape.Demo
{
    public class MaterialsDemoWorld : IDemoWorld
    {
        private readonly IDemoWorld demoWorld;

        public MaterialsDemoWorld(World world) : this(
            world,
            new ProvidedAsset<Material>(new Material(Shader.Find("Standard")!))
        ) { }

        public MaterialsDemoWorld(World world, ProvidedAsset<Material> providedAssetMaterials) : this(
            world,
            new MaterialsPool(providedAssetMaterials),
            FrameTimeCapBudget.NewDefault(),
            MemoryBudget.NewDefault(),
            (in MaterialData _, Material material) => { UnityObjectUtils.SafeDestroy(material); },
            new ISceneData.Fake()
        ) { }

        public MaterialsDemoWorld(
            World world,
            IObjectPool<Material> materialsPool,
            IPerformanceBudget capFrameBudget,
            IPerformanceBudget memoryBudget,
            DestroyMaterial destroyMaterial,
            ISceneData sceneData,
            IExtendedObjectPool<Texture2D> videoTexturePool = null,
            int attemptLoad = 5
        )
        {
            IReadOnlyDictionary<CRDTEntity, Entity> entityMap = new Dictionary<CRDTEntity, Entity>();

            demoWorld = new DemoWorld(
                world,
                w => { },
                w => new StartMaterialsLoadingSystem(w, destroyMaterial, sceneData, attemptLoad, capFrameBudget, entityMap, videoTexturePool),
                w => new CreateBasicMaterialSystem(w, materialsPool, capFrameBudget, memoryBudget),
                w => new CreatePBRMaterialSystem(w, materialsPool, capFrameBudget, memoryBudget),
                w => new ApplyMaterialSystem(w, sceneData),
                w => new ResetMaterialSystem(w, destroyMaterial, sceneData),
                w => new CleanUpMaterialsSystem(w, destroyMaterial, VideoTextureFactory.CreateVideoTexturesPool())
            );
        }

        public void SetUp()
        {
            demoWorld.SetUp();
        }

        public void Update()
        {
            demoWorld.Update();
        }
    }
}
