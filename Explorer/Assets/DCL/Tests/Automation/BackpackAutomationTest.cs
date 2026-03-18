// For more info on the setup, check out our docs https://alttester.com/docs/sdk/latest/pages/get-started.html#write-and-execute-first-test-for-your-app
using AltTester.AltTesterSDK.Driver;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Tests.Automation
{
    public class BackpackAutomationTest
    {
        private AltDriver altDriver;
        public const string JUMP_INTO_WORLD_PATH = "/Authentication.MainScreen(Clone)/Lobby.ExistingAccount.Screen/JumpIntoWorldButton/JumpIntoWorld";
        public const string BACKPACK_BUTTON_PATH = "/MainUIContainer(Clone)/UILayout/Sidebar/SidebarView/UpperLayout/BackpackButton";
        public const string TITLE_PATH = "/ExplorePanelUI(Clone)/AnimationContainer/Sections/BackpackSection/Backpack/Header/Title";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            altDriver = new AltDriver(host: "127.0.0.1", port: 13000, appName: "__default__", secureMode: false);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            altDriver.Stop();
        }

        [Test]
        public void Test()
        {
            var jumpIntoWorld = altDriver.WaitForObject(By.PATH, JUMP_INTO_WORLD_PATH, timeout: 20);
            jumpIntoWorld.Click();

            altDriver.WaitForObject(By.COMPONENT, "SceneLoadingScreenView", timeout: 5);
            altDriver.WaitForObjectNotBePresent(By.COMPONENT, "SceneLoadingScreenView", timeout: 20);

            var backpackButton = altDriver.WaitForObject(By.PATH, BACKPACK_BUTTON_PATH, timeout: 30);
            backpackButton.Click();
            var title = altDriver.WaitForObject(By.PATH, TITLE_PATH, timeout: 20);

            Assert.IsTrue(title.enabled);
        }
    }
}
