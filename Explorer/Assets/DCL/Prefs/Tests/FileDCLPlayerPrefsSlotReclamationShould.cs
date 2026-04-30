using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

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
        public void GrantDistinctSlotsWhenInstancesRaceToClaim()
        {
            const int instanceCount = 8;

            var instances = new FileDCLPlayerPrefs[instanceCount];
            var failures = new Exception[instanceCount];
            var threads = new Thread[instanceCount];
            using var barrier = new Barrier(instanceCount);

            for (var i = 0; i < instanceCount; i++)
            {
                int idx = i;
                threads[idx] = new Thread(() =>
                {
                    try
                    {
                        barrier.SignalAndWait();
                        instances[idx] = new FileDCLPlayerPrefs(tempDir);
                    }
                    catch (Exception e) { failures[idx] = e; }
                });
            }

            try
            {
                foreach (Thread t in threads) t.Start();
                foreach (Thread t in threads) t.Join();

                for (var i = 0; i < instanceCount; i++)
                    Assert.IsNull(failures[i], $"Thread {i} threw: {failures[i]}");

                // FileMode.CreateNew is kernel-atomic — every racing constructor must land on
                // a distinct slot, so the on-disk claim file count must equal the thread count.
                string[] claimFiles = Directory.GetFiles(tempDir, "userdata_*.claim");
                Assert.AreEqual(instanceCount, claimFiles.Length, "Expected one claim file per concurrent instance.");

                using Process self = Process.GetCurrentProcess();

                foreach (string path in claimFiles)
                {
                    var claim = JsonConvert.DeserializeObject<FileDCLPlayerPrefs.SlotClaim>(File.ReadAllText(path));
                    Assert.AreEqual(self.Id, claim.Pid, $"Claim file {path} not owned by current process.");
                }
            }
            finally
            {
                foreach (FileDCLPlayerPrefs prefs in instances)
                    prefs?.Dispose();
            }
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
    }
}
