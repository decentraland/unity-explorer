using Arch.Core;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using Global.Dynamic;
using NSubstitute;
using NUnit.Framework;
using PortableExperiences.Controller;
using Runtime.Wearables;
using System;
using System.Threading;

namespace PortableExperiences.Tests
{
    public class ECSPortableExperiencesControllerShould
    {
        private const string LOCAL_PX_ID = "localpx.dcl.eth";
        private const string GLOBAL_PX_ID = "globalpx.dcl.eth";
        private const string SMART_WEARABLE_PX_ID = "smartwearablepx.dcl.eth";

        private World world = null!;
        private CancellationTokenSource globalWorldCts = null!;
        private LocalPortableExperienceCache localCache = null!;
        private GlobalPortableExperienceCache globalCache = null!;
        private SmartWearableCache smartWearableCache = null!;
        private ECSPortableExperiencesController controller = null!;

        [SetUp]
        public void Setup()
        {
            world = World.Create();
            globalWorldCts = new CancellationTokenSource();

            localCache = new LocalPortableExperienceCache(Substitute.For<IWebRequestController>());
            globalCache = new GlobalPortableExperienceCache();
            smartWearableCache = new SmartWearableCache(Substitute.For<IWebRequestController>());

            controller = new ECSPortableExperiencesController(
                Substitute.For<IWeb3IdentityCache>(),
                Substitute.For<IWebRequestController>(),
                Substitute.For<IScenesCache>(),
                localCache,
                globalCache,
                smartWearableCache,
                Substitute.For<ILaunchMode>(),
                Substitute.For<IDecentralandUrlsSource>());

            controller.GlobalWorld = new GlobalWorld(world, null!, Array.Empty<IFinalizeWorldSystem>(),
                new CameraSamplingData(), new RealmSamplingData(), globalWorldCts);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
            globalWorldCts.Cancel();
            globalWorldCts.Dispose();
        }

        private void SeedPortableExperience(string id, PortableExperienceType type)
        {
            Entity entity = world.Create(new PortableExperienceMetadata
            {
                Type = type,
                Ens = id,
                Id = id,
                Name = id,
                ParentSceneId = "parent-scene",
            });

            controller.AddPortableExperience(id, entity);
        }

        [Test]
        public void RecordKilledLocalPortableExperienceInLocalCacheOnly()
        {
            // Arrange
            SeedPortableExperience(LOCAL_PX_ID, PortableExperienceType.Local);
            localCache.RunningPortableExperiences.Add(LOCAL_PX_ID);

            // Act
            IPortableExperiencesController.ExitResponse response = controller.KillPortableExperienceById(LOCAL_PX_ID);

            // Assert
            Assert.IsTrue(response.status);
            Assert.IsTrue(localCache.KilledPortableExperiences.Contains(LOCAL_PX_ID));
            Assert.IsFalse(localCache.RunningPortableExperiences.Contains(LOCAL_PX_ID));
            Assert.IsEmpty(smartWearableCache.KilledPortableExperiences);
            Assert.IsEmpty(globalCache.KilledPortableExperiences);
        }

        [Test]
        public void RecordKilledGlobalPortableExperienceInGlobalCacheOnly()
        {
            // Arrange
            SeedPortableExperience(GLOBAL_PX_ID, PortableExperienceType.Global);
            globalCache.RunningPortableExperiences.Add(GLOBAL_PX_ID);

            // Act
            IPortableExperiencesController.ExitResponse response = controller.KillPortableExperienceById(GLOBAL_PX_ID);

            // Assert
            Assert.IsTrue(response.status);
            Assert.IsTrue(globalCache.KilledPortableExperiences.Contains(GLOBAL_PX_ID));
            Assert.IsFalse(globalCache.RunningPortableExperiences.Contains(GLOBAL_PX_ID));
            Assert.IsEmpty(smartWearableCache.KilledPortableExperiences);
            Assert.IsEmpty(localCache.KilledPortableExperiences);
        }

        [Test]
        public void RecordKilledSmartWearableInSmartWearableCacheOnly()
        {
            // Arrange
            SeedPortableExperience(SMART_WEARABLE_PX_ID, PortableExperienceType.SmartWearable);

            // Act
            IPortableExperiencesController.ExitResponse response = controller.KillPortableExperienceById(SMART_WEARABLE_PX_ID);

            // Assert
            Assert.IsTrue(response.status);
            Assert.IsTrue(smartWearableCache.KilledPortableExperiences.Contains(SMART_WEARABLE_PX_ID));
            Assert.IsEmpty(localCache.KilledPortableExperiences);
            Assert.IsEmpty(globalCache.KilledPortableExperiences);
        }

        [Test]
        public void NotRecordUnloadedPortableExperienceAsKilled()
        {
            // Arrange
            SeedPortableExperience(LOCAL_PX_ID, PortableExperienceType.Local);
            localCache.RunningPortableExperiences.Add(LOCAL_PX_ID);

            // Act
            IPortableExperiencesController.ExitResponse response = controller.UnloadPortableExperienceById(LOCAL_PX_ID);

            // Assert
            Assert.IsTrue(response.status);
            Assert.IsFalse(localCache.RunningPortableExperiences.Contains(LOCAL_PX_ID));
            Assert.IsEmpty(localCache.KilledPortableExperiences);
            Assert.IsEmpty(smartWearableCache.KilledPortableExperiences);
            Assert.IsEmpty(globalCache.KilledPortableExperiences);
        }

        [Test]
        public void FailToKillUnknownPortableExperienceWithoutMutatingAnyCache()
        {
            // Act
            IPortableExperiencesController.ExitResponse response = controller.KillPortableExperienceById("unknown.dcl.eth");

            // Assert
            Assert.IsFalse(response.status);
            Assert.IsEmpty(localCache.KilledPortableExperiences);
            Assert.IsEmpty(smartWearableCache.KilledPortableExperiences);
            Assert.IsEmpty(globalCache.KilledPortableExperiences);
        }
    }
}
