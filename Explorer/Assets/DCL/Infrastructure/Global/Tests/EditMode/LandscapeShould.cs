using DCL.Ipfs;
using DCL.Landscape;
using DCL.Landscape.Settings;
using DCL.Profiling;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Utility.Types;
using ECS;
using ECS.SceneLifeCycle.Realm;
using Global.Dynamic.Landscapes;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Global.Tests.EditMode
{
    public class LandscapeShould
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task NotAnnounceMissingGenesisTerrain(bool initialized)
        {
            var genesisTerrain = new TerrainGenerator(Substitute.For<IMemoryProfiler>());
            var worldsTerrain = new WorldTerrainGenerator();
            var generationData = ScriptableObject.CreateInstance<TerrainGenerationData>();
            var landscapeData = ScriptableObject.CreateInstance<LandscapeData>();
            IIpfsRealm ipfsRealm = Substitute.For<IIpfsRealm>();
            ipfsRealm.SceneUrns.Returns(Array.Empty<string>());
            var realmData = new RealmData(ipfsRealm);
            IGlobalRealmController realmController = Substitute.For<IGlobalRealmController>();
            realmController.RealmData.Returns(realmData);
            var landscape = new Landscape(realmController, genesisTerrain, worldsTerrain, landscapeEnabled: true);
            var terrainLoaded = false;
            landscape.TerrainLoaded += _ => terrainLoaded = true;
            var report = AsyncLoadProcessReport.Create(CancellationToken.None);

            try
            {
                if (initialized)
                    genesisTerrain.Initialize(generationData, Array.Empty<int>(), landscapeData);

                EnumResult<LandscapeError> result = await landscape.LoadTerrainAsync(report, CancellationToken.None);

                Assert.IsFalse(result.Success);
                Assert.AreEqual(LandscapeError.TerrainDataUnavailable, result.Error!.Value.State);
                Assert.IsFalse(terrainLoaded);
                Assert.IsNull(landscape.CurrentTerrain.TerrainModel);
                Assert.AreEqual(1f, report.ProgressCounter.Value);
                Assert.IsTrue(landscape.IsParcelInsideTerrain(new Vector2Int(1, 0), false).Success);
            }
            finally
            {
                genesisTerrain.Trees?.Dispose();
                genesisTerrain.Dispose();
                worldsTerrain.Dispose();
                Object.DestroyImmediate(landscape.Root.gameObject);
                Object.DestroyImmediate(generationData);
                Object.DestroyImmediate(landscapeData);
            }
        }
    }
}
