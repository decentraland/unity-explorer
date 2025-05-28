using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.Landscape.Settings;

namespace DCL.Landscape.GPUIHelpers
{
// Stub class for when the GPUI package is not used
#if !GPUIPRO_PRESENT
    public class GPUIWrapper
    {
        public void Initialize(LandscapeData landscapeData) { }

        public void Inject(TerrainGenerator terrainGenerator, ref ArchSystemsWorldBuilder<World> worldBuilder, IDebugContainerBuilder debugBuilder) { }
    }
#endif
}