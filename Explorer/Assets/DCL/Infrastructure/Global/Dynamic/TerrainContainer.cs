using DCL.DebugUtilities;
using DCL.Landscape;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using Global.Dynamic.Landscapes;

namespace Global.Dynamic
{
    public class TerrainContainer
    {
        public ILandscape Landscape { get; private init; }

        public TerrainGenerator GenesisTerrain { get; private init; }

        public WorldTerrainGenerator WorldsTerrain { get; private init; }

        private bool landscapeEnabled { get; init; }

        public LandscapePlugin CreatePlugin(StaticContainer staticContainer, BootstrapContainer bootstrapContainer, MapRendererContainer mapRendererContainer,
            IDebugContainerBuilder debugBuilder) =>
            new (staticContainer.RealmData, staticContainer.LoadingStatus, staticContainer.ScenesCache, GenesisTerrain, WorldsTerrain, bootstrapContainer.AssetsProvisioner,
                debugBuilder, mapRendererContainer.TextureContainer,
                staticContainer.WebRequestsContainer.WebRequestController, staticContainer.LandscapeParcelData, staticContainer.LandscapeParcelController, landscapeEnabled,
                bootstrapContainer.Environment.Equals(DecentralandEnvironment.Zone), (Landscape)Landscape);

        public static TerrainContainer Create(StaticContainer staticContainer, RealmContainer realmContainer, bool enableLandscape, bool localSceneDevelopemnt)
        {
            var genesisTerrain = new TerrainGenerator(staticContainer.Profiler);
            var worldsTerrain = new WorldTerrainGenerator();

            return new TerrainContainer
            {
                Landscape = new Landscape(realmContainer.RealmController, genesisTerrain, worldsTerrain, enableLandscape),
                GenesisTerrain = genesisTerrain,
                WorldsTerrain = worldsTerrain,
                landscapeEnabled = enableLandscape,
            };
        }
    }
}
