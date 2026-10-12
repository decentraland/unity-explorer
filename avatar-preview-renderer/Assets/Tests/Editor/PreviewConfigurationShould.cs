using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class PreviewConfigurationShould
    {
        private const float TOLERANCE = 0.0001f;

        [TearDown]
        public void ResetConfiguration()
        {
            PreviewConfiguration.RecreateFrom(string.Empty);
        }

        [Test]
        public void ReadTheOptionsFromTheUrl()
        {
            // Act
            PreviewConfiguration.RecreateFrom(
                "https://preview.example.com/?mode=builder&hideControls=true&zoom=100&wheelZoom=2&wheelStart=100"
                + "&camera=static&lockAlpha=true&lockBeta=on&panning=false&disableAutoRotate=true&autoRotateSpeed=0.5"
                + "&offsetX=1&offsetY=-0.5&showThumbnailBoundaries=true&disableDefaultWearables=true&disableFace=true"
                + "&url=https://example.com/a.json&peerUrl=https://peer.example.com/");
            var config = PreviewConfiguration.Instance;

            // Assert
            Assert.AreEqual(PreviewMode.Builder, config.Mode);
            Assert.IsTrue(config.HideControls);
            Assert.AreEqual(100f, config.ZoomLevel.Value, TOLERANCE);
            Assert.AreEqual(2f, config.WheelZoom.Value, TOLERANCE);
            Assert.AreEqual(100f, config.WheelStart, TOLERANCE);
            Assert.AreEqual(PreviewCameraKind.Static, config.Camera);
            Assert.IsTrue(config.LockAlpha);
            Assert.IsTrue(config.LockBeta);
            Assert.IsFalse(config.LockRadius);
            Assert.IsFalse(config.Panning);
            Assert.IsTrue(config.DisableAutoRotate);
            Assert.AreEqual(0.5f, config.AutoRotateSpeed, TOLERANCE);
            Assert.AreEqual(new Vector2(1f, -0.5f), config.Offset);
            Assert.IsTrue(config.ShowThumbnailBoundaries);
            Assert.IsTrue(config.DisableDefaultWearables);
            Assert.IsTrue(config.DisableFace);
            Assert.AreEqual(new[] { "https://example.com/a.json" }, config.Urls);
            Assert.AreEqual("https://peer.example.com", config.PeerUrl);
            Assert.IsNull(config.MarketplaceServerUrl);
        }

        [Test]
        public void KeepTheDefaultsForUnparsableValues()
        {
            // Act
            PreviewConfiguration.RecreateFrom("?zoom=abc&wheelStart=NaN&camera=statc&hideControls=maybe&offsetX=Infinity");
            var config = PreviewConfiguration.Instance;

            // Assert
            Assert.IsNull(config.ZoomLevel);
            Assert.AreEqual(50f, config.WheelStart, TOLERANCE);
            Assert.AreEqual(PreviewCameraKind.Interactive, config.Camera);
            Assert.IsFalse(config.HideControls);
            Assert.AreEqual(Vector2.zero, config.Offset);
        }

        [Test]
        public void MapDisableDefaultEmotesToNoEmote()
        {
            // Act
            PreviewConfiguration.RecreateFrom("?disableDefaultEmotes=true");

            // Assert
            Assert.AreEqual(PreviewConfiguration.NO_EMOTE, PreviewConfiguration.Instance.Emote);
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        [TestCase("1,5")]
        [TestCase("")]
        public void RejectANonFiniteNumber(string value)
        {
            // Act
            var parsed = PreviewConfiguration.TryParseNumber(value, out _);

            // Assert
            Assert.IsFalse(parsed);
        }
    }
}
