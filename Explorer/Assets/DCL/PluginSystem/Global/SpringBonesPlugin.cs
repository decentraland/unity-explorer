using Arch.SystemGroups;
using DCL.SpringBones;

namespace DCL.PluginSystem.Global
{
    public class SpringBonesPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly SpringBoneService springBoneService = new ();

        public void Dispose()
        {
            springBoneService.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SpringBonesSimulationSystem.InjectToWorld(ref builder, springBoneService);
            SpringBoneRegistrationSystem.InjectToWorld(ref builder, springBoneService);
        }
    }
}
