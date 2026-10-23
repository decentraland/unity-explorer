using NUnit.Framework;
using System.IO;

namespace BevyMigration.Tools.ApiInventory.Tests
{
    public class AllowlistShould
    {
        private string tomlPath = null!;

        [SetUp]
        public void SetUp()
        {
            tomlPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".toml");
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(tomlPath);
        }

        [Test]
        public void RoundTripThroughToml()
        {
            // Arrange
            Allowlist generated = Allowlist.FromInventory(Inventory.FromMembers(new[]
            {
                Use("UnityEngine.GameObject..ctor(string)", "UnityEngine.GameObject"),
                Use("UnityEngine.Material.SetFloat(int,float)", "UnityEngine.Material"),
                Use("UnityEditor.EditorUtility.SetDirty(Object)", "UnityEditor.EditorUtility", "UnityEditor"),
            }));

            // Act
            generated.Write(tomlPath);
            Allowlist read = Allowlist.Read(tomlPath);

            // Assert
            Assert.That(read.Groups.Keys, Is.EquivalentTo(new[] { "object_model", "rendering" }));
            Assert.That(read.Groups["object_model"].Phase, Is.EqualTo("B"));
            Assert.That(read.Groups["rendering"].Phase, Is.EqualTo("A"));
            Assert.That(read.Groups["object_model"].Members, Is.EquivalentTo(new[] { "UnityEngine.GameObject..ctor(string)" }));
            Assert.That(read.Groups["rendering"].RemovedByRefactor, Is.Empty);
        }

        [Test]
        public void FailOnMembersOutsideTheAllowlistAndOnReintroducedOnes()
        {
            // Arrange
            File.WriteAllText(tomlPath, "[object_model]\nphase = \"B\"\nmembers = [\"UnityEngine.GameObject..ctor(string)\", \"UnityEngine.GameObject.name\"]\nremoved_by_refactor = [\"UnityEngine.Animator.GetCurrentAnimatorStateInfo(int)\"]\n\n[ui]\nmembers = []\n");
            Allowlist allowlist = Allowlist.Read(tomlPath);

            Inventory inventory = Inventory.FromMembers(new[]
            {
                Use("UnityEngine.GameObject..ctor(string)", "UnityEngine.GameObject"),
                Use("UnityEngine.Material.SetFloat(int,float)", "UnityEngine.Material"),
                Use("UnityEngine.Animator.GetCurrentAnimatorStateInfo(int)", "UnityEngine.Animator"),
                Use("UnityEditor.EditorUtility.SetDirty(Object)", "UnityEditor.EditorUtility", "UnityEditor"),
            });

            // Act
            AllowlistCheck check = allowlist.Check(inventory);

            // Assert
            Assert.That(check.Passed, Is.False);
            Assert.That(check.Missing, Has.Count.EqualTo(1).And.Exactly(1).Matches<MemberUse>(m => m.Id == "UnityEngine.Material.SetFloat(int,float)"));
            Assert.That(check.Reintroduced, Has.Count.EqualTo(1));
            Assert.That(check.Unused, Is.EquivalentTo(new[] { "UnityEngine.GameObject.name" }));
            Assert.That(check.EditorMembers, Has.Count.EqualTo(1));
            Assert.That(check.GroupsWithoutPhase, Is.EquivalentTo(new[] { "ui" }));
        }

        [Test]
        public void PassWhenEveryUsedMemberIsListed()
        {
            // Arrange
            Allowlist.FromInventory(Inventory.FromMembers(new[] { Use("UnityEngine.Time.deltaTime", "UnityEngine.Time") })).Write(tomlPath);

            // Act
            AllowlistCheck check = Allowlist.Read(tomlPath).Check(Inventory.FromMembers(new[] { Use("UnityEngine.Time.deltaTime", "UnityEngine.Time") }));

            // Assert
            Assert.That(check.Passed, Is.True);
        }

        private static MemberUse Use(string id, string containingType, string root = "UnityEngine")
        {
            var use = new MemberUse(id, "method", root, containingType);
            use.Add("Explorer/Assets/DCL/Sample/File.cs");
            return use;
        }
    }
}
