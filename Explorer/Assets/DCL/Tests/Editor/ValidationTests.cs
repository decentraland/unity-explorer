using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using static Utility.Tests.TestsCategories;

namespace DCL.Tests
{
    public class ValidationTests
    {
        [Category(VALIDATION)]
        [Test]
        public void ProjectShouldNotContainEmptyFolders()
        {
            // Arrange
            string[] allDirectories = Directory.GetDirectories(Application.dataPath, "*", SearchOption.AllDirectories);

            string excludedDirectory = Path.Combine(Application.dataPath, "AddressableAssetsData");
            allDirectories = allDirectories.Where(directory => !directory.StartsWith(excludedDirectory, StringComparison.OrdinalIgnoreCase)).ToArray();

            // Act
            var emptyDirectories = allDirectories.Where(IsDirectoryEmpty).ToList();
            string errorMessage = "Found empty directories:\n" + string.Join("\n", emptyDirectories);

            // Assert
            Assert.That(emptyDirectories.Count, Is.EqualTo(0), errorMessage);
        }

        private static bool IsDirectoryEmpty(string path) =>
            !Directory.EnumerateFileSystemEntries(path).Any();

    }
}
