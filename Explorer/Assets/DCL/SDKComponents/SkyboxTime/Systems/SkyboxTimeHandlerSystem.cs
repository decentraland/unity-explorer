using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.SkyboxTime.Components;
using DCL.SkyBox;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using TransitionMode = DCL.ECSComponents.TransitionMode;

namespace DCL.SDKComponents.SkyboxTime.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class SkyboxTimeHandlerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
    {
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly Entity rootEntity;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly ISceneRestrictionBusController sceneRestrictionController;

        public SkyboxTimeHandlerSystem(World world, SkyboxSettingsAsset skyboxSettings, Entity rootEntity, ISceneStateProvider sceneStateProvider, ISceneRestrictionBusController sceneRestrictionController) : base(world)
        {
            this.skyboxSettings = skyboxSettings;
            this.rootEntity = rootEntity;
            this.sceneStateProvider = sceneStateProvider;
            this.sceneRestrictionController = sceneRestrictionController;
        }

        protected override void Update(float t)
        {
            CreateComponentQuery(World);
            UpdateSkyboxTimeQuery(World);

            HandleComponentRemovalQuery(World);
        }

        [Query]
        [None(typeof(SkyboxTimeComponent))]
        private void CreateComponent(in Entity entity, ref PBSkyboxTime sdkSkyboxTime)
        {
            if(sceneStateProvider.IsCurrent == false) return;

            if (entity.Id != SpecialEntitiesID.SCENE_ROOT_ENTITY)
                World.Remove<PBSkyboxTime>(entity);

            sdkSkyboxTime.IsDirty = true;
            skyboxSettings.IsSDKControlled = true;
            World.Add<SkyboxTimeComponent>(entity);
        }

        [Query]
        [All(typeof(SkyboxTimeComponent))]
        private void UpdateSkyboxTime(ref PBSkyboxTime sdkSkyboxTime)
        {
            if(sceneStateProvider.IsCurrent == false) return;

            if(!sdkSkyboxTime.IsDirty) return;

            SetSDKsettings(sdkSkyboxTime);

            sdkSkyboxTime.IsDirty = false;
        }

        private void SetSDKsettings(PBSkyboxTime sdkSkyboxTime)
        {
            skyboxSettings.IsDayCycleEnabled = false;
            skyboxSettings.TargetTransitionTimeOfDay = sdkSkyboxTime.FixedTimeOfDay;
            skyboxSettings.TransitionMode = sdkSkyboxTime.TransitionMode == TransitionMode.TmForward ? SkyBox.TransitionMode.FORWARD : SkyBox.TransitionMode.BACKWARD;
            skyboxSettings.IsTransitioning = true;

            sceneRestrictionController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.APPLIED));
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBSkyboxTime))]
        [All(typeof(SkyboxTimeComponent))]
        private void HandleComponentRemoval()
        {
            sceneRestrictionController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.REMOVED));
            skyboxSettings.IsSDKControlled = false;
            World.Remove<SkyboxTimeComponent>(rootEntity);
        }

        public void OnSceneIsCurrentChanged(bool value)
        {
            if (value) return;

            HandleComponentRemoval();
        }

        public void FinalizeComponents(in Query query)
        {
            HandleComponentRemoval();
        }
    }
}
