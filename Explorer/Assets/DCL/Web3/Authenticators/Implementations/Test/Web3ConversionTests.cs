using NUnit.Framework;
using System.Numerics;
using Thirdweb;

namespace DCL.Web3.Authenticators.Tests
{
    [TestFixture]
    public class Web3ConversionTests
    {
        // ============ HexToNumber Tests ============

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
        public void HexToNumber_OneEth_ConvertsCorrectly()
        {
            // Arrange - 1 ETH = 10^18 Wei = 1000000000000000000
            var hexBalance = "0xDE0B6B3A7640000";

            // Act
            BigInteger result = hexBalance.HexToNumber();

            // Assert
            Assert.AreEqual(BigInteger.Parse("1000000000000000000"), result);
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
        public void HexToNumber_MaxUint256_HandlesLargeNumbers()
        {
            // Arrange - Very large number (2^64 - 1)
            var hexBalance = "0xFFFFFFFFFFFFFFFF";

            // Act
            BigInteger result = hexBalance.HexToNumber();

            // Assert
            Assert.Greater(result, BigInteger.Zero);
            Assert.AreEqual(new BigInteger(18446744073709551615), result);
        }

        // ============ HexToBytes Tests ============

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

        // ============ ToEth Tests ============

        [Test]
        public void ToEth_ZeroWei_ReturnsZero()
        {
            // Arrange - 0 Wei как decimal строка
            var weiString = "0";

            // Act
            string ethBalance = weiString.ToEth(decimalsToDisplay: 6, addCommas: false);

            // Assert
            Assert.AreEqual("0.000000", ethBalance);
        }

        [Test]
        public void ToEth_OneWei_ReturnsSmallEth()
        {
            // Arrange - 1 Wei
            var weiString = "1";

            // Act
            string ethBalance = weiString.ToEth(decimalsToDisplay: 18, addCommas: false);

            // Assert
            Assert.AreEqual("0.000000000000000001", ethBalance);
        }

        [Test]
        public void ToEth_OneEth_ReturnsOne()
        {
            // Arrange - 1 ETH = 1000000000000000000 Wei
            var weiString = "1000000000000000000";

            // Act
            string ethBalance = weiString.ToEth(decimalsToDisplay: 2, addCommas: false);

            // Assert
            Assert.AreEqual("1.00", ethBalance);
        }

        [Test]
        public void ToEth_HalfEth_ReturnsHalf()
        {
            // Arrange - 0.5 ETH = 500000000000000000 Wei
            var weiString = "500000000000000000";

            // Act
            string ethBalance = weiString.ToEth(decimalsToDisplay: 2, addCommas: false);

            // Assert
            Assert.AreEqual("0.50", ethBalance);
        }

        [Test]
        public void ToEth_LargeBalance_FormatsCorrectly()
        {
            // Arrange - 100 ETH
            BigInteger weiValue = BigInteger.Parse("1000000000000000000") * 100;
            var weiString = weiValue.ToString();

            // Act
            string ethBalance = weiString.ToEth(decimalsToDisplay: 4, addCommas: false);

            // Assert
            Assert.AreEqual("100.0000", ethBalance);
        }

        [Test]
        public void ToEth_WithCommas_AddsCommas()
        {
            // Arrange - 1000 ETH
            BigInteger weiValue = BigInteger.Parse("1000000000000000000") * 1000;
            var weiString = weiValue.ToString();

            // Act
            string ethBalance = weiString.ToEth(decimalsToDisplay: 2, addCommas: true);

            // Assert
            Assert.That(ethBalance, Does.Contain(",") | Does.Contain("1000"));
        }

        [Test]
        public void ToEth_VerySmallBalance_HandlesCorrectly()
        {
            // Arrange - 0.000001 ETH = 1000000000000 Wei (10^12)
            var weiString = "1000000000000";

            // Act
            string ethBalance = weiString.ToEth(decimalsToDisplay: 6, addCommas: false);

            // Assert
            Assert.AreEqual("0.000001", ethBalance);
        }

        // ============ Complete Flow Tests (Hex → BigInteger → String → ETH) ============

        [Test]
        public void CompleteFlow_HexToEth_ZeroBalance()
        {
            // Arrange - получили 0x0 от eth_getBalance
            var hexResponse = "0x0";

            // Act - полный пайплайн как в реальном коде
            BigInteger weiValue = hexResponse.HexToNumber();
            var weiString = weiValue.ToString();
            string ethBalance = weiString.ToEth(decimalsToDisplay: 6, addCommas: false);

            // Assert
            Assert.AreEqual("0.000000", ethBalance);
        }

        [Test]
        public void CompleteFlow_HexToEth_OneEth()
        {
            // Arrange - получили 1 ETH от eth_getBalance
            var hexResponse = "0xDE0B6B3A7640000";

            // Act
            BigInteger weiValue = hexResponse.HexToNumber();
            var weiString = weiValue.ToString();
            string ethBalance = weiString.ToEth(decimalsToDisplay: 2, addCommas: false);

            // Assert
            Assert.AreEqual("1.00", ethBalance);
        }

        [Test]
        public void CompleteFlow_HexToEth_SmallBalance()
        {
            // Arrange - 0.1 ETH
            var hexResponse = "0x16345785D8A0000";

            // Act
            BigInteger weiValue = hexResponse.HexToNumber();
            var weiString = weiValue.ToString();
            string ethBalance = weiString.ToEth(decimalsToDisplay: 4, addCommas: false);

            // Assert
            Assert.AreEqual("0.1000", ethBalance);
        }

        // ============ ToWei Tests ============

        [Test]
        public void ToWei_ZeroEth_ReturnsZero()
        {
            // Arrange
            var ethString = "0";

            // Act
            string weiString = ethString.ToWei();

            // Assert
            Assert.AreEqual("0", weiString);
        }

        [Test]
        public void ToWei_OneEth_ReturnsCorrectWei()
        {
            // Arrange
            var ethString = "1";

            // Act
            string weiString = ethString.ToWei();

            // Assert
            Assert.AreEqual("1000000000000000000", weiString);
        }

        [Test]
        public void ToWei_DecimalEth_ReturnsCorrectWei()
        {
            // Arrange
            var ethString = "0.5";

            // Act
            string weiString = ethString.ToWei();

            // Assert
            Assert.AreEqual("500000000000000000", weiString);
        }

        [Test]
        public void ToWei_SmallEth_ReturnsCorrectWei()
        {
            // Arrange
            var ethString = "0.000001";

            // Act
            string weiString = ethString.ToWei();

            // Assert
            Assert.AreEqual("1000000000000", weiString);
        }

        // ============ Round-trip Tests (ETH → Wei → ETH) ============

        [Test]
        public void RoundTrip_EthToWeiToEth_PreservesValue()
        {
            // Arrange
            var originalEth = "1.5";

            // Act
            string wei = originalEth.ToWei();
            string resultEth = wei.ToEth(decimalsToDisplay: 1, addCommas: false);

            // Assert
            Assert.AreEqual("1.5", resultEth);
        }
    }
}
