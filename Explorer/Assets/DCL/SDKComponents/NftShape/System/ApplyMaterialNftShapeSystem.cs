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
using ECS.StreamableLoading;

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
            throw new Exception("Obsolete system!");
            TryApplyMaterialQuery(World!);
        }

        [Query]
        [All(typeof(PBNftShape))]
        private void TryApplyMaterial(ref MaterialComponent materialComponent, ref NftShapeRendererComponent nftShapeRendererComponent)
        {
            switch (materialComponent.Status)
            {
                // If Material is loaded but not applied
                case LifeCycle.LoadingFinished:
                    materialComponent.Status = LifeCycle.Applied;
                    ConfigureSceneMaterial.EnableSceneBounds(materialComponent.Result!, sceneData.Geometry.CircumscribedPlanes);
                    //nftShapeRendererComponent.ApplyMaterial(materialComponent);
                    break;
                case LifeCycle.LoadingNotStarted: break;
                case LifeCycle.LoadingInProgress: break;
                case LifeCycle.Applied: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}
