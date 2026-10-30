using NUnit.Framework;
using System.IO;

namespace DCL.Prefs.Tests
{
    [TestFixture]
    public class FileDCLPlayerPrefsRecoveryShould
    {
        private const string STAGED_JSON = "{\"Strings\":{\"key\":\"staged\"},\"Ints\":{},\"Floats\":{},\"Bools\":{}}";

        private string tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "DCLPrefsTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(tempDir, true); }
            catch { /* ignored */ }
        }

        [Test]
        public void RecoverStagedCopyWhenLiveFileWasCutShort()
        {
            // Arrange
            File.WriteAllText(DataPath(0), "{\"Strings\":{\"ke");
            File.WriteAllText(StagingPath(0), STAGED_JSON);

            // Act
            using var prefs = new FileDCLPlayerPrefs(tempDir);

            // Assert
            Assert.AreEqual("staged", prefs.GetString("key", string.Empty));
        }

        [Test]
        public void RecoverStagedCopyWhenLiveFileWasLeftEmpty()
        {
            // Arrange
            File.WriteAllText(DataPath(0), string.Empty);
            File.WriteAllText(StagingPath(0), STAGED_JSON);

            // Act
            using var prefs = new FileDCLPlayerPrefs(tempDir);

            // Assert
            Assert.AreEqual("staged", prefs.GetString("key", string.Empty));
        }

        [Test]
        public void IgnoreMalformedStagedCopy()
        {
            // Arrange
            File.WriteAllText(DataPath(0), "{\"Strings\":{\"key\":\"live\"},\"Ints\":{},\"Floats\":{},\"Bools\":{}}");
            File.WriteAllText(StagingPath(0), "{\"Strings\":{\"ke");

            // Act
            using var prefs = new FileDCLPlayerPrefs(tempDir);

            // Assert
            Assert.AreEqual("live", prefs.GetString("key", string.Empty));
        }

        [Test]
        public void LeaveNoStagedCopyAfterCompleteSave()
        {
            // Arrange
            using var prefs = new FileDCLPlayerPrefs(tempDir);
            prefs.SetString("key", "value");

            // Act
            prefs.SaveSync();

            // Assert
            Assert.IsFalse(File.Exists(StagingPath(0)));
            Assert.That(File.ReadAllText(DataPath(0)), Does.Contain("\"key\":\"value\""));
        }

        private string DataPath(int slot) =>
            Path.Combine(tempDir, $"userdata_{slot}.json");

        private string StagingPath(int slot) =>
            Path.Combine(tempDir, $"userdata_{slot}.json.tmp");
    }
}
