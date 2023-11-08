using NUnit.Framework;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DCL.Tests
{
    public class ValidationTests
    {
        [Test]
        public void ProjectShouldNotContainEmptyFolders()
        {
            // Arrange
            string[] allDirectories = Directory.GetDirectories(Application.dataPath, "*", SearchOption.AllDirectories);

            // Act
            var emptyDirectories = allDirectories.Where(IsDirectoryEmpty).ToList();
            string errorMessage = "Found empty directories:\n" + string.Join("\n", emptyDirectories);

            // Assert
            Assert.That(emptyDirectories.Count, Is.EqualTo(0), errorMessage);

            return;

            bool IsDirectoryEmpty(string path) =>
                !Directory.EnumerateFileSystemEntries(path).Any();
        }
    }
}
