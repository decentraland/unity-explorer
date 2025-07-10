using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SkyBox;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using SceneRunner.Scene;
using TransitionMode = DCL.ECSComponents.TransitionMode;

namespace DCL.SDKComponents.SkyboxTime.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class SkyboxTimeHandlerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly Entity rootEntity;
        private readonly ISceneStateProvider sceneStateProvider;

        private SkyboxTimeHandlerSystem(World world, SkyboxSettingsAsset skyboxSettings, Entity rootEntity,
            ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.skyboxSettings = skyboxSettings;
            this.rootEntity = rootEntity;
            this.sceneStateProvider = sceneStateProvider;
        }

        public void OnSceneIsCurrentChanged(bool value)
        {
            // TODO: move part/all of these into SDKComponentState?
            if (value)
            {
                ref PBSkyboxTime sdkSkyboxTime = ref World.TryGetRef<PBSkyboxTime>(rootEntity, out bool hasComponent);

                if (hasComponent)
                    SetSDKsettings(ref sdkSkyboxTime);

                return;
            }

            if (skyboxSettings.IsSDKControlled)
                ResetSDKControlled();
        }

        protected override void Update(float t)
        {
            if (sceneStateProvider.IsCurrent == false) return;

            ref PBSkyboxTime sdkSkyboxTime = ref World.TryGetRef<PBSkyboxTime>(rootEntity, out bool hasComponent);

            if (!hasComponent)
            {
                if (skyboxSettings.IsSDKControlled)
                    ResetSDKControlled();

                return;
            }

            if (!sdkSkyboxTime.IsDirty) return;

            SetSDKsettings(ref sdkSkyboxTime);

            sdkSkyboxTime.IsDirty = false;
        }

        private void SetSDKsettings(ref PBSkyboxTime sdkSkyboxTime)
        {
            skyboxSettings.IsSDKControlled = true;
            skyboxSettings.IsDayCycleEnabled = false;
            skyboxSettings.TargetTimeOfDayNormalized = SkyboxSettingsAsset.NormalizeTime(sdkSkyboxTime.FixedTime);

            skyboxSettings.TransitionMode = sdkSkyboxTime.TransitionMode == TransitionMode.TmForward
                ? SkyBox.TransitionMode.FORWARD
                : SkyBox.TransitionMode.BACKWARD;
        }

        private void ResetSDKControlled()
        {
            skyboxSettings.IsSDKControlled = false;
        }
    }
}
