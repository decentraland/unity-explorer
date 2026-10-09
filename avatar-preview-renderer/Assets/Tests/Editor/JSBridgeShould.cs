using NUnit.Framework;
using UnityEngine;

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

        [TestCase("0,0,0", 0f, 0f, 0f)]
        [TestCase("1.5,-2,3e-1", 1.5f, -2f, 0.3f)]
        [TestCase(" 0.25 , 0.5 , 1 ", 0.25f, 0.5f, 1f)]
        public void ParseAVector(string value, float x, float y, float z)
        {
            // Act
            var parsed = JSBridge.TryParseVector(value, out var vector);

            // Assert
            Assert.IsTrue(parsed);
            Assert.AreEqual(new Vector3(x, y, z), vector);
        }

        [TestCase("")]
        [TestCase("1,2")]
        [TestCase("1,2,3,4")]
        [TestCase("a,b,c")]
        [TestCase("1;2;3")]
        public void RejectAnInvalidVector(string value)
        {
            // Act
            var parsed = JSBridge.TryParseVector(value, out _);

            // Assert
            Assert.IsFalse(parsed);
        }
    }
}
