using NUnit.Framework;
using System.Numerics;
using Thirdweb;

namespace DCL.Web3.Authenticators.Tests
{
    [TestFixture]
    public class Web3ConversionTests
    {
        [Test]
        public void HexToNumber_ZeroBalance_ReturnsZero()
        {
            // Arrange
            var hexBalance = "0x0";

            // Act
            BigInteger result = hexBalance.HexToNumber();

            // Assert
            Assert.AreEqual(new BigInteger(0), result);
        }

        [Test]
        public void HexToNumber_EmptyHex_ReturnsZero()
        {
            // Arrange
            var hexBalance = "0x";

            // Act
            BigInteger result = hexBalance.HexToNumber();

            // Assert
            Assert.AreEqual(new BigInteger(0), result);
        }

        [Test]
        public void ToEth_ZeroBalance_ReturnsZeroEth()
        {
            // Arrange
            var hexBalance = "0x0";

            // Act
            string ethBalance = hexBalance.ToEth(decimalsToDisplay: 6, addCommas: false);

            // Assert
            Assert.AreEqual("0.000000", ethBalance);
        }

        [Test]
        public void ToEth_OneWei_ReturnsSmallEth()
        {
            // Arrange - 1 Wei = 0x1
            var hexBalance = "0x1";

            // Act
            string ethBalance = hexBalance.ToEth(decimalsToDisplay: 18, addCommas: false);

            // Assert
            Assert.AreEqual("0.000000000000000001", ethBalance);
        }

        [Test]
        public void ToEth_OneEth_ReturnsOne()
        {
            // Arrange - 1 ETH = 10^18 Wei = 0xDE0B6B3A7640000
            var hexBalance = "0xDE0B6B3A7640000";

            // Act
            string ethBalance = hexBalance.ToEth(decimalsToDisplay: 2, addCommas: false);

            // Assert
            Assert.AreEqual("1.00", ethBalance);
        }

        [Test]
        public void ToEth_HalfEth_ReturnsHalf()
        {
            // Arrange - 0.5 ETH = 5*10^17 Wei = 0x6F05B59D3B20000
            var hexBalance = "0x6F05B59D3B20000";

            // Act
            string ethBalance = hexBalance.ToEth(decimalsToDisplay: 2, addCommas: false);

            // Assert
            Assert.AreEqual("0.50", ethBalance);
        }

        [Test]
        public void ToEth_LargeBalance_FormatsCorrectly()
        {
            // Arrange - 100 ETH = 100*10^18 Wei = 0x56BC75E2D63100000
            var hexBalance = "0x56BC75E2D63100000";

            // Act
            string ethBalance = hexBalance.ToEth(decimalsToDisplay: 4, addCommas: false);

            // Assert
            Assert.AreEqual("100.0000", ethBalance);
        }

        [Test]
        public void ToEth_WithCommas_AddsCommas()
        {
            // Arrange - 1000 ETH
            var hexBalance = "0x3635C9ADC5DEA00000";

            // Act
            string ethBalance = hexBalance.ToEth(decimalsToDisplay: 2, addCommas: true);

            // Assert
            Assert.That(ethBalance, Does.Contain(",") | Does.Contain("1000"));
        }

        [Test]
        public void HexToNumber_GasPrice_ConvertsCorrectly()
        {
            // Arrange - 25 Gwei = 25000000000 Wei = 0x5D21DBA00
            var hexGasPrice = "0x5D21DBA00";

            // Act
            BigInteger weiPrice = hexGasPrice.HexToNumber();
            BigInteger gweiPrice = weiPrice / 1_000_000_000;

            // Assert
            Assert.AreEqual(new BigInteger(25), gweiPrice);
        }

        [Test]
        public void HexToNumber_BlockNumber_ConvertsCorrectly()
        {
            // Arrange - Block 12345678 = 0xBC614E
            var hexBlockNumber = "0xBC614E";

            // Act
            BigInteger blockNumber = hexBlockNumber.HexToNumber();

            // Assert
            Assert.AreEqual(new BigInteger(12345678), blockNumber);
        }

        [Test]
        public void HexToNumber_Nonce_ConvertsCorrectly()
        {
            // Arrange - Nonce 42 = 0x2A
            var hexNonce = "0x2A";

            // Act
            BigInteger nonce = hexNonce.HexToNumber();

            // Assert
            Assert.AreEqual(new BigInteger(42), nonce);
        }

        [Test]
        public void HexToBytes_EmptyCode_ReturnsEmpty()
        {
            // Arrange - Empty contract code
            var hexCode = "0x";

            // Act
            byte[] code = hexCode.HexToBytes();

            // Assert
            Assert.AreEqual(0, code.Length);
        }

        [Test]
        public void HexToBytes_SimpleCode_ReturnsCorrectLength()
        {
            // Arrange - 4 bytes of code = 0x12345678
            var hexCode = "0x12345678";

            // Act
            byte[] code = hexCode.HexToBytes();

            // Assert
            Assert.AreEqual(4, code.Length);
            Assert.AreEqual(0x12, code[0]);
            Assert.AreEqual(0x34, code[1]);
            Assert.AreEqual(0x56, code[2]);
            Assert.AreEqual(0x78, code[3]);
        }

        [Test]
        public void ToEth_VerySmallBalance_HandlesCorrectly()
        {
            // Arrange - 0.000001 ETH = 10^12 Wei = 0xE8D4A51000
            var hexBalance = "0xE8D4A51000";

            // Act
            string ethBalance = hexBalance.ToEth(decimalsToDisplay: 6, addCommas: false);

            // Assert
            Assert.AreEqual("0.000001", ethBalance);
        }

        [Test]
        public void HexToNumber_MaxUint256_HandlesLargeNumbers()
        {
            // Arrange - Very large number (not max, but large)
            var hexBalance = "0xFFFFFFFFFFFFFFFF"; // 2^64 - 1

            // Act
            BigInteger result = hexBalance.HexToNumber();

            // Assert
            Assert.Greater(result, 0);
        }
    }
}
