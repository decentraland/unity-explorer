using Global.AppArgs;
using NUnit.Framework;

namespace DCL.ApplicationGuards
{
    public class ApplicationVersionGuardShould
    {
        [Test]
        [TestCase("--launcher_anonymous_id", "3f2a", true, TestName = "Launcher analytics id marks a launcher-owned process")]
        [TestCase("--launcher_version", "1.22.0", true, TestName = "Launcher version marks a launcher-owned process")]
        [TestCase("--skip-auth-screen", "true", false, TestName = "Other args leave the process hand-run")]
        public void RecogniseALauncherOwnedProcessByItsArgs(string flag, string value, bool expectedLauncherOwned)
        {
            // Arrange
            IAppArgs appArgs = new ApplicationParametersParser(false, flag, value);

            // Act
            bool launcherOwned = ApplicationVersionGuard.IsLauncherOwned(appArgs);

            // Assert
            Assert.AreEqual(expectedLauncherOwned, launcherOwned);
        }
    }
}
