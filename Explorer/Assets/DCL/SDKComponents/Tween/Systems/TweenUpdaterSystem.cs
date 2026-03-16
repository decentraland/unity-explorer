using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Materials.Components;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using SceneRunner.Scene;

namespace DCL.SDKComponents.Tween
{
    /// <summary>
    /// Handles the update logic of PBTween ONLY (when PBTweenSequence runs in SDK Runtime)
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    [UpdateAfter(typeof(TweenLoaderSystem))]
    [UpdateAfter(typeof(InstantiateTransformSystem))]
    [LogCategory(ReportCategory.TWEEN)]
    public partial class TweenUpdaterSystem : BaseUnityLoopSystem
    {
        private readonly TweenerPool tweenerPool;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;

        public TweenUpdaterSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, TweenerPool tweenerPool, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.tweenerPool = tweenerPool;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            UpdatePBTweenQuery(World);
            UpdateTweenTransformQuery(World);
            UpdateTweenTextureQuery(World);
        }

        [Query]
        private void UpdateTweenTransform(ref SDKTweenComponent sdkTweenComponent, ref SDKTransform sdkTransform, in PBTween pbTween, CRDTEntity sdkEntity, TransformComponent transformComponent)
        {
            TweenSDKComponentHelper.UpdateTweenTransform(ref sdkTweenComponent, ref sdkTransform, in pbTween, sdkEntity, transformComponent, tweenerPool, ecsToCRDTWriter, sceneStateProvider);
        }

        [Query]
        private void UpdateTweenTexture(CRDTEntity sdkEntity, in PBTween pbTween, ref SDKTweenComponent sdkTweenComponent, ref MaterialComponent materialComponent)
        {
            TweenSDKComponentHelper.UpdateTweenTexture(sdkEntity, in pbTween, ref sdkTweenComponent, ref materialComponent, tweenerPool, ecsToCRDTWriter, sceneStateProvider);
        }

        [Query]
        private static void UpdatePBTween(ref PBTween pbTween, ref SDKTweenComponent sdkTweenComponent)
        {
            TweenSDKComponentHelper.UpdatePBTween(ref pbTween, ref sdkTweenComponent);
        }
    }
}
