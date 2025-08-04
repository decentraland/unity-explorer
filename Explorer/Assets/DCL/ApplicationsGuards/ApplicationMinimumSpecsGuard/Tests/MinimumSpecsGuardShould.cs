using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.ApplicationMinimumSpecsGuard.Tests
{
    public class MinimumSpecsGuardShould
    {
        private ISpecProfileProvider profileProvider;
        private ISystemInfoProvider systemInfoProvider;
        private IDriveInfoProvider driveInfoProvider;
        private MinimumSpecsGuard specsGuard;

        [SetUp]
        public void SetUp()
        {
            profileProvider = new DefaultSpecProfileProvider();
            systemInfoProvider = Substitute.For<ISystemInfoProvider>();
            driveInfoProvider = Substitute.For<IDriveInfoProvider>();

            // --- Default "Happy Path" Setup ---
            // System passes all checks by default. Each test will override what it needs to fail.
            systemInfoProvider.OperatingSystem.Returns("Windows 11");
            systemInfoProvider.ProcessorType.Returns("AMD Ryzen 7 5800X");
            systemInfoProvider.GraphicsDeviceName.Returns("NVIDIA GeForce RTX 3080");
            systemInfoProvider.GraphicsMemorySize.Returns(8192); // 8GB VRAM
            systemInfoProvider.SystemMemorySize.Returns(16384); // 16GB RAM

            driveInfoProvider.GetPersistentDataPath().Returns("C:\\Users\\Test\\AppData\\LocalLow\\Decentraland");
            driveInfoProvider.GetDrivesInfo().Returns(new List<Utility.PlatformUtils.DriveData>
            {
                new()
                {
                    Name = "C:\\", AvailableFreeSpace = 100L * 1024 * 1024 * 1024
                } // 100GB free
            });
        }

        private void CreateGuard()
        {
            specsGuard = new MinimumSpecsGuard(profileProvider, systemInfoProvider, driveInfoProvider);
        }

        [Test]
        [TestCase("Windows 8", SpecCategory.OS)]
        [TestCase("Intel Core i3-10100F", SpecCategory.CPU)] // i3 fails
        [TestCase("AMD Ryzen 3 3200G", SpecCategory.CPU)] // Ryzen 3 fails
        [TestCase("NVIDIA GeForce GTX 1660", SpecCategory.GPU)] // GTX fails
        public void FailWhenHardwareIsBelowMinimum(string failingValue, SpecCategory category)
        {
            // Arrange
            switch (category)
            {
                case SpecCategory.OS: systemInfoProvider.OperatingSystem.Returns(failingValue); break;
                case SpecCategory.CPU: systemInfoProvider.ProcessorType.Returns(failingValue); break;
                case SpecCategory.GPU: systemInfoProvider.GraphicsDeviceName.Returns(failingValue); break;
            }

            CreateGuard();

            // Act & Assert
            Assert.IsFalse(specsGuard.HasMinimumSpecs());
            Assert.IsFalse(specsGuard.Results.First(r => r.Category == category).IsMet);
        }

        [Test]
        public void FailWhenVramIsInsufficient()
        {
            systemInfoProvider.GraphicsMemorySize.Returns(4096); // 4GB VRAM
            CreateGuard();
            Assert.IsFalse(specsGuard.HasMinimumSpecs());
            Assert.IsFalse(specsGuard.Results.First(r => r.Category == SpecCategory.VRAM).IsMet);
        }

        [Test]
        public void FailWhenRamIsInsufficient()
        {
            systemInfoProvider.SystemMemorySize.Returns(8192); // 8GB RAM
            CreateGuard();
            Assert.IsFalse(specsGuard.HasMinimumSpecs());
            Assert.IsFalse(specsGuard.Results.First(r => r.Category == SpecCategory.RAM).IsMet);
        }

        // --- Integrated GPU Special Case ---

        [Test]
        public void FailAndShowIntegratedGpuMessage_WhenGpuIsIntegrated()
        {
            // Arrange
            systemInfoProvider.GraphicsDeviceName.Returns("Intel(R) Iris(R) Xe Graphics");
            CreateGuard();
            var profile = profileProvider.GetProfile(PlatformOS.Windows);

            // Act
            Assert.IsFalse(specsGuard.HasMinimumSpecs());
            var gpuResult = specsGuard.Results.First(r => r.Category == SpecCategory.GPU);

            // Assert
            Assert.IsFalse(gpuResult.IsMet);
            Assert.AreEqual(profile.GpuIntegratedRequirement, gpuResult.Required);
            Assert.AreNotEqual(profile.GpuRequirement, gpuResult.Required);
        }

        // --- Storage Failure Tests ---

        [Test]
        public void FailWhenStorageIsInsufficient()
        {
            // Arrange
            driveInfoProvider.GetDrivesInfo().Returns(new List<Utility.PlatformUtils.DriveData>
            {
                new()
                {
                    Name = "C:\\", AvailableFreeSpace = 4L * 1024 * 1024 * 1024
                } // Only 4GB free
            });
            CreateGuard();

            // Act & Assert
            Assert.IsFalse(specsGuard.HasMinimumSpecs());
            Assert.IsFalse(specsGuard.Results.First(r => r.Category == SpecCategory.Storage).IsMet);
        }

        [Test]
        public void FailGracefullyWhenDriveInfoThrowsException()
        {
            // Arrange
            driveInfoProvider.GetDrivesInfo().Throws(new Exception("Native call failed"));
            CreateGuard();

            LogAssert.Expect(LogType.Exception, new Regex(".*"));

            // Act
            Assert.IsFalse(specsGuard.HasMinimumSpecs());
            var storageResult = specsGuard.Results.First(r => r.Category == SpecCategory.Storage);

            // Assert
            Assert.IsFalse(storageResult.IsMet);
            Assert.AreEqual("Error determining space", storageResult.Actual);
        }

        [Test]
        public void FailGracefullyWhenNoDrivesAreFound()
        {
            // Arrange
            driveInfoProvider.GetDrivesInfo().Returns(new List<Utility.PlatformUtils.DriveData>());
            CreateGuard();

            // Act
            Assert.IsFalse(specsGuard.HasMinimumSpecs());
            var storageResult = specsGuard.Results.First(r => r.Category == SpecCategory.Storage);

            // Assert
            Assert.IsFalse(storageResult.IsMet);
            Assert.AreEqual("No drives found", storageResult.Actual);
        }
    }
}