using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS;
using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Applies the realm-level fixed skybox hour from the server about response (configurations.skybox.fixedHour).
    ///     Takes priority over UIOverride and GlobalTime, but yields to SDK components and scene metadata.
    /// </summary>
    public class RealmSkyboxState : ISkyboxState
    {
        private readonly IRealmData realmData;
        private readonly SkyboxSettingsAsset settings;
        private readonly ISceneRestrictionBusController sceneRestrictionController;
        private readonly InterpolateTimeOfDayState transition;

        public RealmSkyboxState(
            IRealmData realmData,
            SkyboxSettingsAsset settings,
            ISceneRestrictionBusController sceneRestrictionController,
            InterpolateTimeOfDayState transition)
        {
            this.realmData = realmData;
            this.settings = settings;
            this.sceneRestrictionController = sceneRestrictionController;
            this.transition = transition;
        }

        public bool Applies() =>
            realmData.Configured && realmData.SkyboxFixedHour.HasValue;

        public void Enter()
        {
            sceneRestrictionController.PushSceneRestriction(
                SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.APPLIED));

            settings.IsDayCycleEnabled = false;
            ApplyFixedHour();
            transition.Enter();
        }

        public void Update(float dt)
        {
            ApplyFixedHour();
            transition.Update(dt);
        }

        public void Exit()
        {
            sceneRestrictionController.PushSceneRestriction(
                SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.REMOVED));

            transition.Exit();
        }

        private void ApplyFixedHour()
        {
            if (!realmData.SkyboxFixedHour.HasValue)
                return;

            float normalizedTime = SkyboxSettingsAsset.NormalizeTime(realmData.SkyboxFixedHour.Value);

            if (Mathf.Approximately(settings.TargetTimeOfDayNormalized, normalizedTime))
                return;

            settings.TargetTimeOfDayNormalized = normalizedTime;
        }
    }
}
