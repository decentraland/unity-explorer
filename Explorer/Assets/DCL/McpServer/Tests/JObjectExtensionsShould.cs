using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DCL.McpServer.Tests
{
    /// <summary>
    ///     The shared argument-diagnosis clause every numeric tool appends to its own "required argument" error.
    /// </summary>
    public class JObjectExtensionsShould
    {
        [Test]
        public void NameOnlyTheArgumentsThatArrivedUnusable()
        {
            var arguments = new JObject { ["x"] = 2393f, ["y"] = "3.0", ["z"] = 2393 };

            string hint = arguments.NonNumericHint("x", "y", "z");

            Assert.That(hint, Is.EqualTo(" (y arrived as string \"3.0\", not a number)"));
        }

        [Test]
        public void NameEveryUnusableArgument()
        {
            var arguments = new JObject { ["x"] = "2393", ["y"] = true, ["z"] = 2393 };

            string hint = arguments.NonNumericHint("x", "y", "z");

            Assert.That(hint, Is.EqualTo(" (x arrived as string \"2393\", not a number; y arrived as boolean true, not a number)"));
        }

        [Test]
        public void SayNothingAboutAbsentOrUsableArguments()
        {
            var arguments = new JObject { ["x"] = 1, ["y"] = 2.5f };

            // An argument the caller never sent is the tool's own message to explain; both number types are usable.
            Assert.That(arguments.NonNumericHint("x", "y", "z"), Is.Empty);
        }

        [Test]
        public void TruncateWhatItEchoesBack()
        {
            var arguments = new JObject { ["x"] = new string('a', 200) };

            string hint = arguments.NonNumericHint("x");

            Assert.That(hint, Does.Contain("…"));
            Assert.That(hint.Length, Is.LessThan(120));
        }
    }
}
