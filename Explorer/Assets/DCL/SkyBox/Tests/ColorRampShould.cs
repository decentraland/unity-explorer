using Decentraland.Common;
using NUnit.Framework;
using UnityEngine;

namespace DCL.SkyBox.Tests
{
    public class ColorRampShould
    {
        private const float TOLERANCE = 1e-5f;

        [Test]
        public void ReturnConstantColorForSingleKey()
        {
            // Arrange
            ColorRamp? ramp = ColorRamp.FromProto(Gradient((0.3f, Color.red)));

            // Act & Assert
            Assert.That(ramp, Is.Not.Null);
            Assert.That(ramp!.KeyCount, Is.EqualTo(1));
            AssertColor(ramp.Evaluate(0f), Color.red);
            AssertColor(ramp.Evaluate(0.3f), Color.red);
            AssertColor(ramp.Evaluate(1f), Color.red);
        }

        [Test]
        public void ClampBeforeFirstAndAfterLastKey()
        {
            // Arrange
            ColorRamp ramp = Ramp((0.25f, Color.red), (0.75f, Color.blue));

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
            ColorRamp ramp = Ramp((0f, Color.black), (1f, Color.white));

            // Act
            Color mid = ramp.Evaluate(0.5f);

            // Assert
            AssertColor(mid, new Color(0.5f, 0.5f, 0.5f, 1f));
        }

        [Test]
        public void SortUnsortedKeysByTime()
        {
            // Arrange
            ColorRamp ramp = Ramp((1f, Color.white), (0f, Color.black), (0.5f, Color.red));

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
            ColorRamp ramp = Ramp((0f, Color.black), (0.5f, Color.red), (0.5f, Color.blue), (1f, Color.white));

            // Act & Assert
            AssertColor(ramp.Evaluate(0.5f), Color.blue);
            AssertColor(ramp.Evaluate(0.75f), new Color(0.5f, 0.5f, 1f, 1f));
            AssertColor(ramp.Evaluate(0.25f), new Color(0.5f, 0f, 0f, 1f));
        }

        [Test]
        public void PreserveHdrValues()
        {
            // Arrange
            var hdr = new Color(2.5f, 1.5f, 0f, 1f);
            ColorRamp ramp = Ramp((0f, hdr), (1f, hdr));

            // Act & Assert
            AssertColor(ramp.Evaluate(0.5f), hdr);
            Assert.That(ramp.Evaluate(0f).r, Is.EqualTo(2.5f).Within(TOLERANCE));
        }

        [Test]
        public void ClampKeyTimesToUnitRange()
        {
            // Arrange
            ColorRamp ramp = Ramp((-1f, Color.black), (2f, Color.white));

            // Act & Assert
            AssertColor(ramp.Evaluate(0.5f), new Color(0.5f, 0.5f, 0.5f, 1f));
        }

        [Test]
        public void ReturnNullForNullGradient()
        {
            // Act & Assert
            Assert.That(ColorRamp.FromProto(null), Is.Null);
        }

        [Test]
        public void ReturnNullForEmptyGradient()
        {
            // Act & Assert
            Assert.That(ColorRamp.FromProto(new ColorGradient()), Is.Null);
        }

        [Test]
        public void SkipKeysWithoutColor()
        {
            // Arrange
            ColorGradient gradient = Gradient((0f, Color.red));
            gradient.Keys.Add(new ColorKey { Time = 1f });

            // Act
            ColorRamp? ramp = ColorRamp.FromProto(gradient);

            // Assert
            Assert.That(ramp, Is.Not.Null);
            Assert.That(ramp!.KeyCount, Is.EqualTo(1));
            AssertColor(ramp.Evaluate(1f), Color.red);
        }

        [Test]
        public void ReturnNullWhenNoKeyHasColor()
        {
            // Arrange
            var gradient = new ColorGradient { Keys = { new ColorKey { Time = 0f }, new ColorKey { Time = 1f } } };

            // Act & Assert
            Assert.That(ColorRamp.FromProto(gradient), Is.Null);
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

        private static ColorRamp Ramp(params (float time, Color color)[] keys)
        {
            ColorRamp? ramp = ColorRamp.FromProto(Gradient(keys));
            Assert.That(ramp, Is.Not.Null);
            return ramp!;
        }
    }
}
