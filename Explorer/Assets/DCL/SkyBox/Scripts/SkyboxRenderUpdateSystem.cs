using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using JetBrains.Annotations;

namespace DCL.SkyBox
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SkyboxRenderUpdateSystem : BaseUnityLoopSystem
    {
        private SkyboxRenderController skyboxRenderController;
        private SkyboxSettingsAsset skyboxSettings;

        public SkyboxRenderUpdateSystem([NotNull] World world, SkyboxRenderController skyboxRenderController, SkyboxSettingsAsset skyboxSettings) : base(world)
        {
            this.skyboxRenderController = skyboxRenderController;
            this.skyboxSettings = skyboxSettings;
        }

        protected override void Update(float _)
        {
            if (skyboxSettings.ShouldUpdateSkybox)
            {
                skyboxRenderController.UpdateSkybox(skyboxSettings.TimeOfDayNormalized);
                skyboxSettings.ShouldUpdateSkybox = false;
            }
        }
    }
}
