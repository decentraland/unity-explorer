using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Groups;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;

namespace DCL.SDKComponents.SceneUI.Systems.UIInput
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UIInputReleaseSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool componentPool;

        internal UIInputReleaseSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            poolsRegistry.TryGetPool(typeof(UIInputComponent), out componentPool);
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleUIInputRemovalQuery(World);
        }

        public void FinalizeComponents(in Query query) =>
            ReleaseFontsQuery(World);

        [Query]
        [None(typeof(PBUiInput), typeof(DeleteEntityIntention))]
        private void HandleUIInputRemoval(in Entity entity, ref UIInputComponent uiInputComponent) =>
            RemoveTextField(entity, uiInputComponent);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(in Entity entity, ref UIInputComponent uiInputComponent, in DeleteEntityIntention deleteEntityIntention)
        {
            if (deleteEntityIntention.DeferDeletion)
                return;

            RemoveTextField(entity, uiInputComponent);
        }

        [Query]
        private void ReleaseFonts(ref UIInputComponent uiInputComponent) =>
            ReleaseFont(uiInputComponent);

        private void RemoveTextField(Entity entity, UIInputComponent uiInputComponent)
        {
            ReleaseFont(uiInputComponent);
            componentPool.Release(uiInputComponent);

            //Removing here the component to avoid double release to the pool in ReleaseReferenceComponentsSystem
            World.Remove<UIInputComponent>(entity);
        }

        private void ReleaseFont(UIInputComponent uiInputComponent) =>
            UiElementUtils.ReleaseCustomFont(World, ref uiInputComponent.FontRequest, ref uiInputComponent.CustomFont, uiInputComponent.TextField);
    }
}
