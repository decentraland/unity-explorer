#if UNITY_EDITOR_LINUX
using NUnit.Framework;
using System;

namespace DCL.VideoPlayback.Tests
{
    public class BmpFrameShould
    {
        private static byte[] Header(int width, int height, int pixelOffset = BmpFrame.HEADER_BYTES, ushort bitsPerPixel = 32)
        {
            var header = new byte[BmpFrame.HEADER_BYTES];
            header[0] = (byte)'B';
            header[1] = (byte)'M';
            int pixelBytes = Math.Abs(width) * Math.Abs(height) * 4;
            BitConverter.GetBytes(pixelOffset + pixelBytes).CopyTo(header, 2);
            BitConverter.GetBytes(pixelOffset).CopyTo(header, 10);
            BitConverter.GetBytes(40).CopyTo(header, 14);
            BitConverter.GetBytes(width).CopyTo(header, 18);
            BitConverter.GetBytes(height).CopyTo(header, 22);
            BitConverter.GetBytes((ushort)1).CopyTo(header, 26);
            BitConverter.GetBytes(bitsPerPixel).CopyTo(header, 28);
            BitConverter.GetBytes(pixelBytes).CopyTo(header, 34);
            return header;
        }

        [Test]
        public void ParseBottomUpHeader()
        {
            // Act
            bool ok = BmpFrame.TryParseHeader(Header(640, 360), out BmpFrame.Header header, out string error);

            // Assert
            Assert.IsTrue(ok, error);
            Assert.AreEqual(640, header.Width);
            Assert.AreEqual(360, header.Height);
            Assert.IsFalse(header.TopDown);
            Assert.AreEqual(BmpFrame.HEADER_BYTES, header.PixelOffset);
            Assert.AreEqual(640 * 360 * 4, header.PixelBytes);
        }

        [Test]
        public void ParseTopDownHeaderAndLargerDibBlock()
        {
            // Act
            bool ok = BmpFrame.TryParseHeader(Header(4, -2, 122), out BmpFrame.Header header, out _);

            // Assert
            Assert.IsTrue(ok);
            Assert.AreEqual(2, header.Height);
            Assert.IsTrue(header.TopDown);
            Assert.AreEqual(122, header.PixelOffset);
        }

        [Test]
        public void RejectForeignSignaturesAndDepths()
        {
            // Arrange
            byte[] wrongSignature = Header(4, 4);
            wrongSignature[0] = (byte)'X';

            // Act & Assert
            Assert.IsFalse(BmpFrame.TryParseHeader(wrongSignature, out _, out string signatureError));
            StringAssert.Contains("signature", signatureError);
            Assert.IsFalse(BmpFrame.TryParseHeader(Header(4, 4, BmpFrame.HEADER_BYTES, 24), out _, out string depthError));
            StringAssert.Contains("depth", depthError);
            Assert.IsFalse(BmpFrame.TryParseHeader(new byte[10], out _, out _));
        }

        [Test]
        public void SwizzleBgraToRgbaKeepingBottomUpRows()
        {
            // Arrange: 2x2, bottom-up; row 0 (bottom) = red, blue; row 1 (top) = green, white.
            BmpFrame.TryParseHeader(Header(2, 2), out BmpFrame.Header header, out _);
            byte[] bgra =
            {
                0, 0, 255, 255, 255, 0, 0, 255,
                0, 255, 0, 255, 255, 255, 255, 255,
            };
            var rgba = new byte[16];

            // Act
            BmpFrame.ConvertToRgba(bgra, header, rgba);

            // Assert
            CollectionAssert.AreEqual(new byte[] { 255, 0, 0, 255 }, new ArraySegment<byte>(rgba, 0, 4));
            CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, new ArraySegment<byte>(rgba, 4, 4));
            CollectionAssert.AreEqual(new byte[] { 0, 255, 0, 255 }, new ArraySegment<byte>(rgba, 8, 4));
            CollectionAssert.AreEqual(new byte[] { 255, 255, 255, 255 }, new ArraySegment<byte>(rgba, 12, 4));
        }

        [Test]
        public void KeepBgraOrderWhenCopyingWithoutSwizzle()
        {
            // Arrange: 1x2 top-down; first stored row (top) = red, second (bottom) = blue.
            BmpFrame.TryParseHeader(Header(1, -2), out BmpFrame.Header header, out _);
            byte[] bgra = { 0, 0, 255, 255, 255, 0, 0, 255 };
            var copy = new byte[8];

            // Act
            BmpFrame.CopyBgra(bgra, header, copy);

            // Assert: rows flipped to bottom-up, channels untouched.
            CollectionAssert.AreEqual(new byte[] { 255, 0, 0, 255 }, new ArraySegment<byte>(copy, 0, 4));
            CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, new ArraySegment<byte>(copy, 4, 4));
        }

        [Test]
        public void FlipTopDownRowsIntoBottomUpOrder()
        {
            // Arrange: 1x2 top-down; first stored row (top) = red, second (bottom) = blue.
            BmpFrame.TryParseHeader(Header(1, -2), out BmpFrame.Header header, out _);
            byte[] bgra = { 0, 0, 255, 255, 255, 0, 0, 255 };
            var rgba = new byte[8];

            // Act
            BmpFrame.ConvertToRgba(bgra, header, rgba);

            // Assert: bottom row first.
            CollectionAssert.AreEqual(new byte[] { 0, 0, 255, 255 }, new ArraySegment<byte>(rgba, 0, 4));
            CollectionAssert.AreEqual(new byte[] { 255, 0, 0, 255 }, new ArraySegment<byte>(rgba, 4, 4));
        }
    }
}
#endif
