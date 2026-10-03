using DCL.Utility;
using NUnit.Framework;
using System;
using System.IO;
using UnityEngine;

namespace Utility.Tests
{
    public class LinuxDriveInfoShould
    {
        [SetUp]
        public void RequireLinux()
        {
            if (Application.platform is not (RuntimePlatform.LinuxEditor or RuntimePlatform.LinuxPlayer))
                Assert.Ignore("Linux statvfs coverage requires a Linux process.");
        }

        [Test]
        public void QueryVolumeContainingProject()
        {
            // Act
            PlatformUtils.DriveData? result = PlatformUtils.GetDriveInfoForPath(Application.dataPath);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.TotalSize, Is.GreaterThan(0));
            Assert.That(result.AvailableFreeSpace, Is.LessThanOrEqualTo(result.TotalSize));
        }

        [Test]
        public void ReturnNoDataForMissingPath()
        {
            // Arrange
            string missingPath = Path.Combine(Path.GetTempPath(), $"dcl-drive-test-{Guid.NewGuid():N}");

            // Act
            PlatformUtils.DriveData? result = PlatformUtils.GetDriveInfoForPath(missingPath);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ReturnNoDataForEmptyPath()
        {
            // Act
            PlatformUtils.DriveData? result = PlatformUtils.GetDriveInfoForPath(string.Empty);

            // Assert
            Assert.That(result, Is.Null);
        }
    }
}
