using Global.Dynamic;
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
                "decentraland://?realm=http://127.0.0.1:8000&position=100,100&local-scene=true",
            });

            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            Assert.IsTrue(realmLaunchSettings.IsLocalSceneDevelopmentRealm);
            Assert.AreEqual("http://127.0.0.1:8000", realmLaunchSettings.GetLocalSceneDevelopmentRealm());
            Assert.AreEqual(100, realmLaunchSettings.TargetScene.x);
            Assert.AreEqual(100, realmLaunchSettings.TargetScene.y);
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

            Assert.IsFalse(realmLaunchSettings.IsLocalSceneDevelopmentRealm);
            Assert.AreEqual("http://127.0.0.1:8000", realmLaunchSettings.GetStartingRealm());
            Assert.AreEqual(70, realmLaunchSettings.TargetScene.x);
            Assert.AreEqual(70, realmLaunchSettings.TargetScene.y);
        }

        [Test]
        public void ApplyStartingPositionFromAppArgs()
        {
            RealmLaunchSettings realmLaunchSettings = new RealmLaunchSettings();
            ApplicationParametersParser applicationParametersParser = new (new[]
            {
                "--position",
                "50,50",
            });

            realmLaunchSettings.ApplyConfig(applicationParametersParser);

            Assert.AreEqual(50, realmLaunchSettings.TargetScene.x);
            Assert.AreEqual(50, realmLaunchSettings.TargetScene.y);
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

            Assert.AreEqual(realm, realmLaunchSettings.GetStartingRealm());
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

            Assert.AreEqual($"https://worlds-content-server.decentraland.org/world/{world}", realmLaunchSettings.GetStartingRealm());
        }
    }
}
