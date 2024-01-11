using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SDKComponents.NftShape.Component;
using ECS.Abstract;
using ECS.Unity.Materials;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using ECS.Unity.SceneBoundsChecker;
using SceneRunner.Scene;
using System;

namespace DCL.SDKComponents.NftShape.System
{
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    [UpdateAfter(typeof(CreateBasicMaterialSystem))]
    [UpdateAfter(typeof(CreatePBRMaterialSystem))]
    public partial class ApplyMaterialNftShapeSystem : BaseUnityLoopSystem
    {
        private readonly ISceneData sceneData;

        public ApplyMaterialNftShapeSystem(World world, ISceneData sceneData) : base(world)
        {
            this.sceneData = sceneData;
        }

        protected override void Update(float t)
        {
            TryApplyMaterialQuery(World!);
        }

        [Query]
        [All(typeof(PBNftShape))]
        private void TryApplyMaterial(ref MaterialComponent materialComponent, ref NftShapeRendererComponent nftShapeRendererComponent)
        {
            switch (materialComponent.Status)
            {
                // If Material is loaded but not applied
                case MaterialComponent.LifeCycle.LoadingFinished:
                    materialComponent.Status = MaterialComponent.LifeCycle.MaterialApplied;
                    ConfigureSceneMaterial.EnableSceneBounds(materialComponent.Result!, sceneData.Geometry.CircumscribedPlanes);
                    nftShapeRendererComponent.ApplyMaterial(materialComponent);
                    break;
                case MaterialComponent.LifeCycle.LoadingNotStarted: break;
                case MaterialComponent.LifeCycle.LoadingInProgress: break;
                case MaterialComponent.LifeCycle.MaterialApplied: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}
