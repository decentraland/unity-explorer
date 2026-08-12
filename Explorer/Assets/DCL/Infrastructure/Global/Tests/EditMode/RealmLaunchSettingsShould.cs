using DCL.Browser.DecentralandUrls;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility;
using DCL.WebRequests;
using Global.AppArgs;
using Global.Dynamic;
using Global.Dynamic.RealmUrl;
using Global.Dynamic.RealmUrl.Names;
using NUnit.Framework;

namespace Global.Tests.EditMode
{
    public class RealmLaunchSettingsShould
    {
        [Test]
        public void ApplyDeeplinkOnDevelopmentMode()
        {
            RealmLaunchSettings realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                "decentraland://?realm=http://127.0.0.1:8000&position=100,100&local-scene=true"
            });

            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            var dclUrlSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, realmLaunchSettings);
            var realmUrls = new RealmUrls(realmLaunchSettings, new RealmNamesMap(IWebRequestController.TEST), dclUrlSource);

            Assert.IsTrue(realmLaunchSettings.CurrentMode is LaunchMode.LocalSceneDevelopment);
            Assert.AreEqual("http://127.0.0.1:8000", realmUrls.LocalSceneDevelopmentRealmBlocking()!);
            Assert.AreEqual(100, realmLaunchSettings.targetScene.x);
            Assert.AreEqual(100, realmLaunchSettings.targetScene.y);
        }

        [Test]
        public void DoNotSetDevelopmentModeIfMissingLocalSceneParam()
        {
            RealmLaunchSettings realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                "decentraland://?realm=http://127.0.0.1:8000&position=70,70",
            });

            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            var dclUrlSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, realmLaunchSettings);
            var realmUrls = new RealmUrls(realmLaunchSettings, new RealmNamesMap(IWebRequestController.TEST), dclUrlSource);

            Assert.IsFalse(realmLaunchSettings.CurrentMode is LaunchMode.LocalSceneDevelopment);
            Assert.AreEqual("http://127.0.0.1:8000", realmUrls.StartingRealmBlocking());
            Assert.AreEqual(70, realmLaunchSettings.targetScene.x);
            Assert.AreEqual(70, realmLaunchSettings.targetScene.y);
        }

        [Test]
        public void ApplyStartingPositionFromAppArgs()
        {
            var realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                "--position",
                "50,50"
            });

            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            Assert.AreEqual(50, realmLaunchSettings.targetScene.x);
            Assert.AreEqual(50, realmLaunchSettings.targetScene.y);
        }

        [Test]
        public void ApplySpawnPointFromDeeplink()
        {
            //Arrange
            var realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                "decentraland://?realm=http://127.0.0.1:8000&position=100,100&spawnpoint=lobby"
            });

            //Act
            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            //Assert
            Assert.AreEqual("lobby", realmLaunchSettings.spawnPointName);
        }

        [Test]
        public void ApplySpawnPointFromAppArgs()
        {
            //Arrange
            var realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                "--spawnpoint",
                "lobby"
            });

            //Act
            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            //Assert
            Assert.AreEqual("lobby", realmLaunchSettings.spawnPointName);
        }

        [Test]
        public void IgnoreEmptySpawnPointFromAppArgs()
        {
            //Arrange
            var realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                "--spawnpoint"
            });

            //Act
            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            //Assert
            Assert.IsNull(realmLaunchSettings.spawnPointName);
        }

        [TestCase("https://peer.decentraland.zone")]
        [TestCase("https://sdk-team-cdn.decentraland.org/ipfs/goerli-plaza-main-latest")]
        public void ApplyStartingRealmFromAppArgs(string realm)
        {
            RealmLaunchSettings realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                "--realm",
                realm,
            });

            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            var dclUrlSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, realmLaunchSettings);
            var realmUrls = new RealmUrls(realmLaunchSettings, new RealmNamesMap(IWebRequestController.TEST), dclUrlSource);

            Assert.AreEqual(realm, realmUrls.StartingRealmBlocking());
        }

        [TestCase("metadyne.dcl.eth")]
        [TestCase("dialogic.dcl.eth")]
        public void ApplyWorldFromAppArgs(string world)
        {
            RealmLaunchSettings realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                "--realm",
                world,
            });

            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            var dclUrlSource = DecentralandUrlsSource.CreateForTest(DecentralandEnvironment.Org, realmLaunchSettings);
            var realmUrls = new RealmUrls(realmLaunchSettings, new RealmNamesMap(IWebRequestController.TEST), dclUrlSource);

            Assert.AreEqual($"https://worlds-content-server.decentraland.org/world/{world}", realmUrls.StartingRealmBlocking());
        }

        [Test]
        [TestCase("metadyne.dcl.eth")]
        [TestCase("dialogic.dcl.eth")]
        public void IgnoreWindowsRealmInvalidation(string world)
        {
            RealmLaunchSettings realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                $"decentraland://realm={world}/", // WinOS on some occasions adds that final '/'
            });

            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            Assert.AreEqual(world, realmLaunchSettings.TargetWorld);
        }

        [TestCase("http://127.0.0.1:8000", "http://127.0.0.1:8000")]
        [TestCase("http://127.0.0.1:8000/", "http://127.0.0.1:8000")] // trailing slash must be trimmed
        [TestCase("http://localhost:8001", "http://localhost:8001")]
        public void DeriveLocalSceneRealmRootFromRealm(string realm, string expectedUrl)
        {
            //Arrange
            var realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                $"decentraland://?realm={realm}&position=100,100&local-scene=true&local-ab=true",
            });

            //Act
            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            //Assert
            Assert.IsTrue(realmLaunchSettings.useLocalAssetBundles, "local-ab must enable local asset bundles");
            Assert.AreEqual(expectedUrl, realmLaunchSettings.LocalSceneRealmRoot());
        }

        [Test]
        public void NotDeriveLocalSceneRealmRootOutsideLocalSceneDevelopment()
        {
            //Arrange
            var realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                "--realm",
                "https://peer.decentraland.org",
            });

            //Act
            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            //Assert
            Assert.IsNull(realmLaunchSettings.LocalSceneRealmRoot());
        }

        [Test]
        [TestCase("127.0.0.1:8000")]
        [TestCase("localhost:8000")]
        public void IgnoreMacOSRealmInvalidation(string realm)
        {
            RealmLaunchSettings realmLaunchSettings = new RealmLaunchSettings();

            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                $"decentraland://realm=http//{realm}", // MacOS removes the ':' from the realm url param
            });

            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            Assert.AreEqual($"http://{realm}", realmLaunchSettings.customRealm);
        }
    }
}
