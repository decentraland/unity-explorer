using NUnit.Framework;

namespace Tests
{
    public class JSBridgeShould
    {
        [TestCase("1,1", 1, 1)]
        [TestCase("1024,768", 1024, 768)]
        [TestCase("4096,4096", 4096, 4096)]
        [TestCase(" 512 , 256 ", 512, 256)]
        public void ParseAValidScreenshotSize(string size, int expectedWidth, int expectedHeight)
        {
            // Act
            var parsed = JSBridge.TryParseScreenshotSize(size, out var width, out var height);

            // Assert
            Assert.IsTrue(parsed);
            Assert.AreEqual(expectedWidth, width);
            Assert.AreEqual(expectedHeight, height);
        }

        [TestCase("")]
        [TestCase("1024")]
        [TestCase("1,2,3")]
        [TestCase("a,b")]
        [TestCase("0,5")]
        [TestCase("5,0")]
        [TestCase("-1,5")]
        [TestCase("4097,1")]
        [TestCase("1,4097")]
        [TestCase("1.5,2")]
        public void RejectAnInvalidScreenshotSize(string size)
        {
            // Act
            var parsed = JSBridge.TryParseScreenshotSize(size, out _, out _);

            // Assert
            Assert.IsFalse(parsed);
        }
    }
}
