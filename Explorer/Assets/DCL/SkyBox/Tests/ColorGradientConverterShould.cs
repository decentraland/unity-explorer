using Decentraland.Common;
using NUnit.Framework;
using UnityEngine;

namespace DCL.SkyBox.Tests
{
    public class ColorGradientConverterShould
    {
        // Unity stores gradient key times with 16-bit precision, so a key evaluated at its own time is off by up to 1/65536.
        private const float TOLERANCE = 1e-4f;

        [Test]
        public void ReturnConstantColorForSingleKey()
        {
            // Arrange
            Gradient? ramp = ColorGradientConverter.ToGradient(Gradient((0.3f, Color.red)));

            // Act & Assert
            Assert.That(ramp, Is.Not.Null);
            AssertColor(ramp!.Evaluate(0f), Color.red);
            AssertColor(ramp.Evaluate(0.3f), Color.red);
            AssertColor(ramp.Evaluate(1f), Color.red);
        }

        [Test]
        public void ClampBeforeFirstAndAfterLastKey()
        {
            // Arrange
            Gradient ramp = Ramp((0.25f, Color.red), (0.75f, Color.blue));

            // Act & Assert
            AssertColor(ramp.Evaluate(0f), Color.red);
            AssertColor(ramp.Evaluate(0.1f), Color.red);
            AssertColor(ramp.Evaluate(0.9f), Color.blue);
            AssertColor(ramp.Evaluate(1f), Color.blue);
        }

        [Test]
        public void InterpolateLinearlyBetweenKeys()
        {
            // Arrange
            Gradient ramp = Ramp((0f, Color.black), (1f, Color.white));

            // Act
            Color mid = ramp.Evaluate(0.5f);

            // Assert
            AssertColor(mid, new Color(0.5f, 0.5f, 0.5f, 1f));
        }

        [Test]
        public void SortUnsortedKeysByTime()
        {
            // Arrange
            Gradient ramp = Ramp((1f, Color.white), (0f, Color.black), (0.5f, Color.red));

            // Act & Assert
            AssertColor(ramp.Evaluate(0f), Color.black);
            AssertColor(ramp.Evaluate(0.5f), Color.red);
            AssertColor(ramp.Evaluate(1f), Color.white);
            AssertColor(ramp.Evaluate(0.25f), new Color(0.5f, 0f, 0f, 1f));
        }

        [Test]
        public void LetLaterKeyWinOnDuplicateTimes()
        {
            // Arrange
            Gradient ramp = Ramp((0f, Color.black), (0.5f, Color.red), (0.5f, Color.blue), (1f, Color.white));

            // Act & Assert
            Assert.That(ramp.colorKeys.Length, Is.EqualTo(3));
            AssertColor(ramp.Evaluate(0.5f), Color.blue);
            AssertColor(ramp.Evaluate(0.75f), new Color(0.5f, 0.5f, 1f, 1f));
        }

        [Test]
        public void PreserveHdrValues()
        {
            // Arrange
            var hdr = new Color(2.5f, 1.5f, 0f, 1f);
            Gradient ramp = Ramp((0f, hdr), (1f, hdr));

            // Act & Assert
            AssertColor(ramp.Evaluate(0.5f), hdr);
            Assert.That(ramp.Evaluate(0f).r, Is.EqualTo(2.5f).Within(TOLERANCE));
        }

        [Test]
        public void IgnoreAlpha()
        {
            // Arrange
            Gradient ramp = Ramp((0f, new Color(1f, 0f, 0f, 0.2f)));

            // Act & Assert
            Assert.That(ramp.Evaluate(0.5f).a, Is.EqualTo(1f).Within(TOLERANCE));
        }

        [Test]
        public void ClampKeyTimesToUnitRange()
        {
            // Arrange
            Gradient ramp = Ramp((-1f, Color.black), (2f, Color.white));

            // Act & Assert
            AssertColor(ramp.Evaluate(0.5f), new Color(0.5f, 0.5f, 0.5f, 1f));
        }

        [Test]
        public void ResampleGradientsWithMoreKeysThanUnitySupports()
        {
            // Arrange: 12 keys, black to white in equal steps, so the resampled ramp must stay the same straight line
            var keys = new (float, Color)[12];

            for (var i = 0; i < keys.Length; i++)
            {
                float t = i / 11f;
                keys[i] = (t, new Color(t, t, t, 1f));
            }

            // Act
            Gradient ramp = Ramp(keys);

            // Assert
            Assert.That(ramp.colorKeys.Length, Is.EqualTo(ColorGradientConverter.MAX_KEYS));
            AssertColor(ramp.Evaluate(0f), Color.black);
            AssertColor(ramp.Evaluate(0.3f), new Color(0.3f, 0.3f, 0.3f, 1f));
            AssertColor(ramp.Evaluate(1f), Color.white);
        }

        [Test]
        public void KeepClampedEndsWhenResampling()
        {
            // Arrange: 9 keys packed into the middle of the range; the ends must still hold the first and last colors
            var keys = new (float, Color)[9];

            for (var i = 0; i < keys.Length; i++)
                keys[i] = (0.4f + (i * 0.025f), i == 0 ? Color.red : i == 8 ? Color.blue : Color.green);

            // Act
            Gradient ramp = Ramp(keys);

            // Assert
            Assert.That(ramp.colorKeys.Length, Is.EqualTo(ColorGradientConverter.MAX_KEYS));
            AssertColor(ramp.Evaluate(0f), Color.red);
            AssertColor(ramp.Evaluate(1f), Color.blue);
        }

        [Test]
        public void ReturnNullForNullGradient()
        {
            // Act & Assert
            Assert.That(ColorGradientConverter.ToGradient(null), Is.Null);
        }

        [Test]
        public void ReturnNullForEmptyGradient()
        {
            // Act & Assert
            Assert.That(ColorGradientConverter.ToGradient(new ColorGradient()), Is.Null);
        }

        [Test]
        public void SkipKeysWithoutColor()
        {
            // Arrange
            ColorGradient gradient = Gradient((0f, Color.red));
            gradient.Keys.Add(new ColorKey { Time = 1f });

            // Act
            Gradient? ramp = ColorGradientConverter.ToGradient(gradient);

            // Assert
            Assert.That(ramp, Is.Not.Null);
            AssertColor(ramp!.Evaluate(0f), Color.red);
            AssertColor(ramp.Evaluate(1f), Color.red);
        }

        [Test]
        public void ReturnNullWhenNoKeyHasColor()
        {
            // Arrange
            var gradient = new ColorGradient { Keys = { new ColorKey { Time = 0f }, new ColorKey { Time = 1f } } };

            // Act & Assert
            Assert.That(ColorGradientConverter.ToGradient(gradient), Is.Null);
        }

        internal static ColorGradient Gradient(params (float time, Color color)[] keys)
        {
            var gradient = new ColorGradient();

            foreach ((float time, Color color) in keys)
                gradient.Keys.Add(new ColorKey { Time = time, Color = new Color4 { R = color.r, G = color.g, B = color.b, A = color.a } });

            return gradient;
        }

        internal static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(TOLERANCE), "r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(TOLERANCE), "g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(TOLERANCE), "b");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(TOLERANCE), "a");
        }

        private static Gradient Ramp(params (float time, Color color)[] keys)
        {
            Gradient? ramp = ColorGradientConverter.ToGradient(Gradient(keys));
            Assert.That(ramp, Is.Not.Null);
            return ramp!;
        }
    }
}
