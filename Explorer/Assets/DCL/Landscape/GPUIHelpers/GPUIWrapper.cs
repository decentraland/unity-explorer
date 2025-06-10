// Stub class for when the GPUI package is not used
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;

namespace DCL.Landscape
{
#if !GPUIPRO_PRESENT
    //Stub class for when GPUI is not present
    public class GPUIWrapper
    {
        public void InjectDebugSystem(ref ArchSystemsWorldBuilder<World> worldBuilder, IDebugContainerBuilder debugBuilder)
        {
        }

        public void SetupLandscapeData(LandscapeData landscapeData)
        {
        }

        public void SetupLocalCache(TerrainGeneratorLocalCache localCache)
        {
        }

        public async UniTask TerrainsInstantiatedAsync(ChunkModel[] terrainModelChunkModels)
        {
        }
    }
#endif
}
