// Stub class for when the GPUI package is not used
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using System;

namespace DCL.Landscape
{
    public interface IGPUIWrapper
    {
        void InjectDebugSystem(ref ArchSystemsWorldBuilder<World> worldBuilder, IDebugContainerBuilder debugBuilder);

        void SetupLandscapeData(LandscapeData landscapeData);

        public void SetupLocalCache(TerrainGeneratorLocalCache localCache);

        UniTask TerrainsInstantiatedAsync(ChunkModel[] terrainModelChunkModels);

        ITerrainDetailSetter GetDetailSetter();
    }

    //Stub class for when GPUI is not present
    public class MockGPUIWrapper : IGPUIWrapper
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

        public UniTask TerrainsInstantiatedAsync(ChunkModel[] terrainModelChunkModels) =>
            UniTask.CompletedTask;

        public ITerrainDetailSetter GetDetailSetter() =>
            new CPUTerrainDetailSetter();
    }
}
