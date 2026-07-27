#if MCP_TEST_AUTOMATION
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace DCL.McpServer.Tests
{
    /// <summary>
    ///     <see cref="InputTestFixture" /> so the state events a press queues land on an isolated Input System rather
    ///     than on the keyboard the Editor is using.
    /// </summary>
    public class McpKeyboardInputShould : InputTestFixture
    {
        /// <summary>
        ///     The held set is static and outlives a fixture, so everything pressed here is released again. A future
        ///     fixture that presses keys owes the same.
        /// </summary>
        [TearDown]
        public void ReleaseHeldKeys()
        {
            McpKeyboardInput.Release(Key.A);
            McpKeyboardInput.Release(Key.B);
        }

        [TestCase("Escape", Key.Escape)]
        [TestCase("escape", Key.Escape)]
        [TestCase("  Space  ", Key.Space)]
        [TestCase("x", Key.X)]
        [TestCase("F5", Key.F5)]
        [TestCase("LeftShift", Key.LeftShift)]
        public void ResolveInputSystemKeyNamesCaseInsensitively(string name, Key expected)
        {
            Assert.That(McpKeyboardInput.TryResolveKey(name, out Key key), Is.True);
            Assert.That(key, Is.EqualTo(expected));
        }

        // "42" and "X,Space" both parse through Enum.TryParse, and neither names the single key a caller meant.
        [TestCase("42")]
        [TestCase("X,Space")]
        [TestCase("NotAKey")]
        [TestCase("None")]
        [TestCase("")]
        [TestCase(null)]
        public void RejectWhatDoesNotNameASingleKey(string? name)
        {
            Assert.That(McpKeyboardInput.TryResolveKey(name, out Key key), Is.False);
            Assert.That(key, Is.EqualTo(Key.None));
        }

        [Test]
        public void KeepEveryPressedKeyDownUntilItsOwnRelease()
        {
            // Arrange — a chord, which is the whole reason press_key has separate down and up legs.
            McpKeyboardInput.Press(Key.A);
            McpKeyboardInput.Press(Key.B);
            Assert.That(McpKeyboardInput.HeldKeys().Count, Is.EqualTo(2));

            // Act
            McpKeyboardInput.Release(Key.A);

            // Assert
            JArray held = McpKeyboardInput.HeldKeys();
            Assert.That(held.Count, Is.EqualTo(1), "releasing one key must not drop the rest of the chord");
            Assert.That(held[0].Value<string>(), Is.EqualTo(nameof(Key.B)));
        }

        [Test]
        public void HoldNothingOnceEveryKeyIsReleased()
        {
            // Arrange
            McpKeyboardInput.Press(Key.A);

            // Act — the second release names a key that was never pressed, which must not disturb the set.
            McpKeyboardInput.Release(Key.A);
            McpKeyboardInput.Release(Key.B);

            // Assert
            Assert.That(McpKeyboardInput.HeldKeys(), Is.Empty);
        }
    }
}
#endif
