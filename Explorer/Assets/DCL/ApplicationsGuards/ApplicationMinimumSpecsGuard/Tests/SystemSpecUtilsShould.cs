using NUnit.Framework;

namespace DCL.ApplicationMinimumSpecsGuard.Tests
{
    // The name of the class clearly states what is being tested.
    public class SystemSpecUtilsShould
    {
        // --- Windows CPU Tests ---

        [Test]
        [TestCase("Intel(R) Core(TM) Ultra 9 185H", true, TestName = "Intel(R) Core(TM) Ultra 9 185H is accepted")]
        [TestCase("Intel Core Ultra 7 165H", true, TestName = "Intel Core Ultra 7 165H is accepted")]
        [TestCase("Intel Core Ultra 5 135U", true, TestName = "Intel Core Ultra 5 135U is accepted")]
        [TestCase("Intel(R) Core(TM) Ultra 7 Processor 155H", true, TestName = "Intel Core Ultra 7 Processor 155H is accepted")]
        [TestCase("Intel(R) Core(TM) i9-9980HK CPU @ 2.40GHz", true, TestName = "Intel(R) Core(TM) i9-9980HK CPU @ 2.40GHz is accepted")]
        [TestCase("Intel(R) Core(TM) i7-8700K CPU @ 3.70GHz", true, TestName = "Intel i7 (8th Gen) is accepted")]
        [TestCase("12th Gen Intel(R) Core(TM) i7-12650H", true, TestName = "12th Gen Intel(R) Core(TM) i7-12650H is accepted")]
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
        public void ValidateWindowsCpu(string cpuName, bool expectedResult)
        {
            // Act: Call the method being tested.
            bool isAcceptable = SystemSpecUtils.IsWindowsCpuAcceptable(cpuName);

            // Assert: Verify the result is what we expect.
            Assert.AreEqual(expectedResult, isAcceptable);
        }

        // --- Windows GPU Tests ---

        [Test]
        [TestCase("NVIDIA GeForce RTX 2060", true, TestName = "NVIDIA RTX 2060 is accepted")]
        [TestCase("NVIDIA GeForce RTX 4070 Laptop GPU", true, TestName = "NVIDIA GeForce RTX 4070 Laptop GPU is accepted")]
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
        public void ValidateWindowsGpu(string gpuName, bool expectedResult)
        {
            // Act
            bool isAcceptable = SystemSpecUtils.IsWindowsGpuAcceptable(gpuName);

            // Assert
            Assert.AreEqual(expectedResult, isAcceptable);
        }

        // Mac Device Tests

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
        public void ValidateAppleSilicon(string deviceName, bool expectedResult)
        {
            // Act
            bool isAcceptable = SystemSpecUtils.IsAppleSilicon(deviceName);

            // Assert
            Assert.AreEqual(expectedResult, isAcceptable);
        }
        
        [Test]
        // --- Standard Cases (Originals) ---
        [TestCase(16280, 16384, true, TestName = "Memory Check - 15.9GB (reported) for 16GB (required) should PASS")]
        [TestCase(8100, 16384, false, TestName = "Memory Check - 7.9GB (reported) for 16GB (required) should FAIL")]
        [TestCase(8100, 8192, true, TestName = "Memory Check - 7.9GB (reported) for 8GB (required) should PASS")]
        [TestCase(4000, 4096, true, TestName = "Memory Check - 3.9GB (reported) for 4GB (required) should PASS")]
        [TestCase(16384, 16384, true, TestName = "Memory Check - Exact 16GB for 16GB (required) should PASS")]

        // --- Rounding Edge Cases (Key to the fix) ---
        [TestCase(15871, 16384, false, TestName = "Rounding - 15.499GB (rounds down to 15) for 16GB should FAIL")]
        [TestCase(15872, 16384, true, TestName = "Rounding - 15.5GB (rounds up to 16) for 16GB should PASS")]
        [TestCase(16077, 16384, true, TestName = "Rounding - 15.7GB (rounds up to 16) for 16GB should PASS")]
        [TestCase(7679, 8192, false, TestName = "Rounding - 7.499GB (rounds down to 7) for 8GB should FAIL")]
        [TestCase(7680, 8192, true, TestName = "Rounding - 7.5GB (rounds up to 8) for 8GB should PASS")]
        
        // --- High Memory Values ---
        [TestCase(32650, 32768, true, TestName = "High Memory - 31.8GB (reported) for 32GB (required) should PASS")]
        [TestCase(65000, 65536, false, TestName = "High Memory - 63.4GB (reported) for 64GB (required) should FAIL")]
        [TestCase(16383, 32768, false, TestName = "High Memory - Almost 16GB for 32GB (required) should FAIL")]

        // --- Low Memory Values (e.g., VRAM on integrated cards) ---
        [TestCase(2000, 2048, true, TestName = "Low Memory - 1.95GB (reported) for 2GB (required) should PASS")]
        [TestCase(1500, 2048, false, TestName = "Low Memory - 1.46GB (reported) for 2GB (required) should FAIL")]
        [TestCase(1000, 1024, true, TestName = "Low Memory - 0.97GB (reported) for 1GB (required) should PASS")]

        // --- Identical Reported and Required Values ---
        [TestCase(8192, 8192, true, TestName = "Exact Match - 8GB exactly for 8GB (required) should PASS")]
        [TestCase(4096, 4096, true, TestName = "Exact Match - 4GB exactly for 4GB (required) should PASS")]

        // --- Values Just Above and Below Requirement ---
        [TestCase(16385, 16384, true, TestName = "Slightly Above - 16.001GB for 16GB (required) should PASS")]
        [TestCase(15871, 16384, false, TestName = "Slightly Below - 15.499GB for 16GB (required) should FAIL (as tested above)")]
        
        // --- Zero and Unusual Values ---
        [TestCase(0, 4096, false, TestName = "Zero Value - 0MB for 4GB (required) should FAIL")]
        [TestCase(4096, 0, true, TestName = "Zero Requirement - 4GB for 0GB (required) should PASS")]
        [TestCase(0, 0, true, TestName = "Zero for Zero - 0MB for 0MB (required) should PASS")]
        
        // --- Non-standard Requirement Values ---
        [TestCase(6000, 6144, true, TestName = "Non-Standard - 5.86GB (reported) for 6GB (required) should PASS")] // 5.86 rounds to 6
        [TestCase(5500, 6144, false, TestName = "Non-Standard - 5.37GB (reported) for 6GB (required) should FAIL")] // 5.37 rounds to 5
        public void ValidateMemorySizeSufficient(int actualMB, int requiredMB, bool expectedResult)
        {
            // Act: Call the method being tested.
            bool isSufficient = SystemSpecUtils.IsMemorySizeSufficient(actualMB, requiredMB);

            // Assert: Verify the result is what we expect.
            Assert.AreEqual(expectedResult, isSufficient, $"Failed on actual: {actualMB}MB, required: {requiredMB}MB");
        }
        
        [Test]
        // --- Standard Discrete GPU VRAM ---
        [TestCase(8100, true, TestName = "VRAM Check - 8GB Card (e.g., RTX 2070) should PASS")]
        [TestCase(6088, true, TestName = "VRAM Check - 6GB Card (e.g., RTX 2060, reported as 5.9GB) should PASS")]
        [TestCase(4050, false, TestName = "VRAM Check - 4GB Card (e.g., GTX 1650) should FAIL")]
        [TestCase(12150, true, TestName = "VRAM Check - 12GB Card (e.g., RTX 3060) should PASS")]

        // --- Apple Silicon Unified Memory ---
        // An 8GB Mac cannot dedicate 6GB to VRAM, so it must fail. This is a critical test.
        [TestCase(10922, true, TestName = "VRAM Check - Apple M1/M2 16GB Mac (reports ~10.6GB) should PASS")]
        [TestCase(5461, false, TestName = "VRAM Check - Apple M1/M2 8GB Mac (reports ~5.3GB) should FAIL")]

        // --- Integrated Graphics (Intel/AMD APU) ---
        // These will always fail a 6GB check.
        [TestCase(2048, false, TestName = "VRAM Check - Integrated GPU (2GB allocated) should FAIL")]
        [TestCase(512, false, TestName = "VRAM Check - Integrated GPU (512MB allocated) should FAIL")]

        // --- Rounding Edge Cases (for 6GB requirement) ---
        // This tests the critical 5.5GB boundary.
        [TestCase(5631, false, TestName = "VRAM Check - Rounding 5.499GB (rounds down to 5) for 6GB should FAIL")]
        [TestCase(5632, true, TestName = "VRAM Check - Rounding 5.5GB (rounds up to 6) for 6GB should PASS")]
        public void ValidateVramSizeSufficient(int actualVramMB, bool expectedResult)
        {
            // Arrange: The requirement for this test suite is 6GB.
            const int requiredVramMB = 6144; // 6 * 1024

            // Act: Call the method being tested.
            bool isSufficient = SystemSpecUtils.IsMemorySizeSufficient(actualVramMB, requiredVramMB);

            // Assert: Verify the result is what we expect.
            Assert.AreEqual(expectedResult, isSufficient, $"Failed on VRAM actual: {actualVramMB}MB, required: {requiredVramMB}MB");
        }
    }
}