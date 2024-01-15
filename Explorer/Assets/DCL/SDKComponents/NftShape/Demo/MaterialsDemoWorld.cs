using Arch.Core;
using DCL.AssetsProvision;
using DCL.DemoWorlds;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.Unity.Materials;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Pooling;
using ECS.Unity.Materials.Systems;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.SDKComponents.NftShape.Demo
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
            new FrameTimeCapBudget(),
            new MemoryBudget(new StandaloneSystemMemory(), new ProfilingProvider()),
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
            int attemptLoad = 5
        )
        {
            demoWorld = new DemoWorld(
                world,
                w => { },
                w => new StartMaterialsLoadingSystem(w, destroyMaterial, sceneData, attemptLoad, capFrameBudget),
                w => new CreateBasicMaterialSystem(w, materialsPool, capFrameBudget, memoryBudget),
                w => new CreatePBRMaterialSystem(w, materialsPool, capFrameBudget, memoryBudget),
                w => new ApplyMaterialSystem(w, sceneData),
                w => new ResetMaterialSystem(w, destroyMaterial, sceneData),
                w => new CleanUpMaterialsSystem(w, destroyMaterial)
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
