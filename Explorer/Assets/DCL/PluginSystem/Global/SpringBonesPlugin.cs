using Arch.SystemGroups;
using DCL.SpringBones;

namespace DCL.PluginSystem.Global
{
    public class SpringBonesPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly SpringBoneService springBoneService = new ();
        private readonly SpringBoneSimulationSettings simulationSettings;

        public SpringBonesPlugin(SpringBoneSimulationSettings simulationSettings)
        {
            this.simulationSettings = simulationSettings;
        }

        public void Dispose()
        {
            springBoneService.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SpringBonesSimulationSystem.InjectToWorld(ref builder, springBoneService, simulationSettings);
            SpringBoneRegistrationSystem.InjectToWorld(ref builder, springBoneService);
        }
    }
}
