using Newtonsoft.Json;
using NUnit.Framework;
using System.Diagnostics;
using System.IO;

namespace DCL.Prefs.Tests
{
    [TestFixture]
    public class FileDCLPlayerPrefsSlotReclamationShould
    {
        private string tempDir;

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
        public void ClaimSlotZeroWhenNoClaimFilesExist()
        {
            using var prefs = new FileDCLPlayerPrefs(tempDir);

            Assert.AreEqual(0, FileDCLPlayerPrefs.PrefsInstanceNumber);
            Assert.IsTrue(File.Exists(ClaimPath(0)));
        }

        [Test]
        public void ReclaimSlotWhenOwnerPidIsDead()
        {
            // Seed slot 0 with a claim pointing to an impossible PID.
            WriteClaim(0, new FileDCLPlayerPrefs.SlotClaim
            {
                Pid = int.MaxValue,
                ProcessName = "ghost",
                MainModulePath = "",
            });
            File.WriteAllText(DataPath(0), "{}");

            using var prefs = new FileDCLPlayerPrefs(tempDir);

            Assert.AreEqual(0, FileDCLPlayerPrefs.PrefsInstanceNumber);
            AssertClaimBelongsToCurrentProcess(0);
        }

        [Test]
        public void ReclaimSlotWhenClaimFileIsMalformed()
        {
            File.WriteAllText(ClaimPath(0), "this is not json {{{");

            using var prefs = new FileDCLPlayerPrefs(tempDir);

            Assert.AreEqual(0, FileDCLPlayerPrefs.PrefsInstanceNumber);
            AssertClaimBelongsToCurrentProcess(0);
        }

        [Test]
        public void FallThroughToSlotOneWhenSlotZeroIsHeldByLiveProcess()
        {
            using Process self = Process.GetCurrentProcess();

            WriteClaim(0, new FileDCLPlayerPrefs.SlotClaim
            {
                Pid = self.Id,
                ProcessName = self.ProcessName,
                MainModulePath = SafeMainModule(self),
            });

            using var prefs = new FileDCLPlayerPrefs(tempDir);

            Assert.AreEqual(1, FileDCLPlayerPrefs.PrefsInstanceNumber);
            AssertClaimBelongsToCurrentProcess(1);
        }

        [Test]
        public void DisposeReleasesClaimFile()
        {
            var prefs = new FileDCLPlayerPrefs(tempDir);
            Assert.AreEqual(0, FileDCLPlayerPrefs.PrefsInstanceNumber);
            Assert.IsTrue(File.Exists(ClaimPath(0)));

            prefs.Dispose();

            Assert.IsFalse(File.Exists(ClaimPath(0)));
        }

        [Test]
        public void NewInstanceCanReclaimSlotAfterDispose()
        {
            var first = new FileDCLPlayerPrefs(tempDir);
            first.SetString("token", "first-identity");
            first.SaveSync();
            first.Dispose();

            using var second = new FileDCLPlayerPrefs(tempDir);

            Assert.AreEqual(0, FileDCLPlayerPrefs.PrefsInstanceNumber);
            Assert.AreEqual("first-identity", second.GetString("token", ""));
        }

        private string ClaimPath(int slot) =>
            Path.Combine(tempDir, $"userdata_{slot}.claim");

        private string DataPath(int slot) =>
            Path.Combine(tempDir, $"userdata_{slot}.json");

        private void WriteClaim(int slot, FileDCLPlayerPrefs.SlotClaim claim) =>
            File.WriteAllText(ClaimPath(slot), JsonConvert.SerializeObject(claim));

        private void AssertClaimBelongsToCurrentProcess(int slot)
        {
            string json = File.ReadAllText(ClaimPath(slot));
            var claim = JsonConvert.DeserializeObject<FileDCLPlayerPrefs.SlotClaim>(json);
            using Process self = Process.GetCurrentProcess();
            Assert.AreEqual(self.Id, claim.Pid);
        }

        private static string SafeMainModule(Process p)
        {
            try { return p.MainModule?.FileName ?? ""; }
            catch { return ""; }
        }
    }
}
