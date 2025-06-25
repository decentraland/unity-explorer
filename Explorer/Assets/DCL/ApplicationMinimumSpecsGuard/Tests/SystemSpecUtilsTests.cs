using NUnit.Framework;

namespace DCL.ApplicationMinimumSpecsGuard.Tests
{
    // The name of the class clearly states what is being tested.
    public class SystemSpecUtilsTests
    {
        // --- Windows CPU Tests ---

        [Test]
        [TestCase("Intel(R) Core(TM) i7-8700K CPU @ 3.70GHz", true, TestName = "Intel i7 (8th Gen) is accepted")]
        [TestCase("Intel Core i5-7600", true, TestName = "Intel i5 (7th Gen) is accepted")]
        [TestCase("Intel Core i9-13900F", true, TestName = "Intel i9 (13th Gen) is accepted")]
        [TestCase("Intel(R) Core(TM) i5-6600 CPU @ 3.30GHz", false, TestName = "Intel i5 (6th Gen) is rejected")]
        [TestCase("Intel Core i3-10100", false, TestName = "Intel i3 is rejected")]
        [TestCase("AMD Ryzen 5 3600 6-Core Processor", true, TestName = "AMD Ryzen 5 is accepted")]
        [TestCase("AMD Ryzen 7 5800X", true, TestName = "AMD Ryzen 7 is accepted")]
        [TestCase("AMD Ryzen 9 7950X", true, TestName = "AMD Ryzen 9 is accepted")]
        [TestCase("AMD Ryzen 3 3200G", false, TestName = "AMD Ryzen 3 is rejected")]
        [TestCase("Intel(R) Core(TM) i5-12400F", true, TestName = "Intel i5 (12th Gen) is accepted")]
        [TestCase("Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz", true, TestName = "Intel i9 (9th Gen) is accepted")]
        [TestCase("Intel(R) Core(TM) i7-4790K CPU @ 4.00GHz", false, TestName = "Intel i7 (4th Gen) is rejected")]
        [TestCase("AMD Ryzen Threadripper 3990X", true, TestName = "AMD Threadripper is accepted")]
        [TestCase("Apple M2", false, TestName = "Apple M2 is rejected on Windows")]
        [TestCase("Intel Pentium Silver N6000", false, TestName = "Intel Pentium is rejected")]
        [TestCase("Intel Celeron J4125", false, TestName = "Intel Celeron is rejected")]
        [TestCase("AMD FX(tm)-8350 Eight-Core Processor", false, TestName = "Legacy AMD FX is rejected")]
        [TestCase("An Unknown Old CPU", false, TestName = "An unknown CPU is rejected")]
        public void WindowsCpuCheck(string cpuName, bool expectedResult)
        {
            // Act: Call the method being tested.
            bool isAcceptable = SystemSpecUtils.IsWindowsCpuAcceptable(cpuName);

            // Assert: Verify the result is what we expect.
            Assert.AreEqual(expectedResult, isAcceptable);
        }

        // --- Windows GPU Tests ---

        [Test]
        [TestCase("NVIDIA GeForce RTX 2060", true, TestName = "NVIDIA RTX 2060 is accepted")]
        [TestCase("NVIDIA GeForce RTX 3080 Ti", true, TestName = "NVIDIA RTX 3080 is accepted")]
        [TestCase("NVIDIA GeForce RTX 4090", true, TestName = "NVIDIA RTX 4090 is accepted")]
        [TestCase("NVIDIA GeForce GTX 1080", false, TestName = "NVIDIA GTX series is rejected")]
        [TestCase("AMD Radeon RX 5700 XT", true, TestName = "AMD RX 5700 XT is accepted")]
        [TestCase("AMD Radeon RX 6600", true, TestName = "AMD RX 6600 is accepted")]
        [TestCase("AMD Radeon RX 580 Series", false, TestName = "AMD RX 580 (below 5000 series) is rejected")]
        [TestCase("NVIDIA GeForce RTX 3050", true, TestName = "NVIDIA RTX 3050 is accepted")]
        [TestCase("NVIDIA GeForce MX450", false, TestName = "NVIDIA MX series is rejected")]
        [TestCase("AMD Radeon RX Vega 56", false, TestName = "AMD Vega series is rejected")]
        [TestCase("Intel Arc A770", true, TestName = "Intel Arc A770 is accepted")]
        [TestCase("Intel Iris Xe Graphics", false, TestName = "Intel Iris Xe (integrated) is rejected")]
        [TestCase("NVIDIA Tesla V100", false, TestName = "NVIDIA Tesla series is rejected (non-gaming)")]
        [TestCase("Microsoft Basic Display Adapter", false, TestName = "Fallback display adapter is rejected")]
        [TestCase("Intel(R) UHD Graphics 630", false, TestName = "Intel integrated graphics is rejected")]
        public void WindowsGpuCheck(string gpuName, bool expectedResult)
        {
            // Act
            bool isAcceptable = SystemSpecUtils.IsWindowsGpuAcceptable(gpuName);

            // Assert
            Assert.AreEqual(expectedResult, isAcceptable);
        }

        // --- Mac Device Tests ---

        [Test]
        [TestCase("Apple M1", true, TestName = "Apple M1 is accepted")]
        [TestCase("Apple M1 Pro", true, TestName = "Apple M1 Pro is accepted")]
        [TestCase("Apple M2 Max", true, TestName = "Apple M2 Max is accepted")]
        [TestCase("Apple M3", true, TestName = "Apple M3 is accepted (future-proof)")]
        [TestCase("Intel Core i7", false, TestName = "Intel-based Mac is rejected")]
        [TestCase("Apple M1 Ultra", true, TestName = "Apple M1 Ultra is accepted")]
        [TestCase("Apple M2 Pro", true, TestName = "Apple M2 Pro is accepted")]
        [TestCase("Apple M4", true, TestName = "Apple M4 is accepted (future-proof)")]
        [TestCase("Apple T2", false, TestName = "Apple T2 security chip (Intel Mac) is rejected")]
        [TestCase("Apple A12Z", false, TestName = "Apple A-series (iPad) is rejected")]
        [TestCase("Rosetta 2 Intel", false, TestName = "Intel under Rosetta is rejected")]
        [TestCase("Intel(R) Core(TM) i5-8279U", false, TestName = "Intel MacBook Pro (2019) is rejected")]
        [TestCase("AMD Radeon Pro 5500M", false, TestName = "AMD GPU on Intel-based Mac is rejected")]
        public void AppleSiliconCheck(string deviceName, bool expectedResult)
        {
            // Act
            bool isAcceptable = SystemSpecUtils.IsAppleSilicon(deviceName);

            // Assert
            Assert.AreEqual(expectedResult, isAcceptable);
        }
    }
}