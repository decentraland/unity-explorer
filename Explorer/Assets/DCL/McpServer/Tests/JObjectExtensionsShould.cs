using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Diagnostics.CodeAnalysis;

namespace DCL.McpServer.Tests
{
    /// <summary>
    ///     The shared argument diagnosis the tools report through: the clause a numeric tool appends to its own
    ///     "required argument" error, and the whole error an enum argument is refused with.
    /// </summary>
    public class JObjectExtensionsShould
    {
        /// <summary>Stands in for a tool's wire-facing enum; the wire values derive from these member names.</summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum WireStack : byte
        {
            UGUI,
            SDK,
        }

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
        public void NameTheEnumValueThatArrivedAndTheOnesAccepted()
        {
            var arguments = new JObject { ["stack"] = "SDK" };

            string error = arguments.EnumArgumentError<WireStack>("stack");

            // The right word in the wrong case: a bare "must be one of: ugui, sdk" leaves the caller unable to
            // see what was wrong with what it sent.
            Assert.That(error, Does.Contain("string \"SDK\""));
            Assert.That(error, Does.Contain("ugui, sdk"));
            Assert.That(error, Does.Not.Contain("required"));
        }

        [Test]
        public void AskForAnEnumArgumentThatIsAbsent()
        {
            string error = new JObject().EnumArgumentError<WireStack>("stack");

            Assert.That(error, Does.Contain("required"));
            Assert.That(error, Does.Contain("ugui, sdk"));
            Assert.That(error, Does.Not.Contain("arrived"), "nothing arrived to echo back");
        }

        [Test]
        public void ListOnlyTheEnumValuesAToolExposes()
        {
            // A tool that exposes a subset (set_camera_mode, walk) must not advertise the members it refuses.
            string whole = new JObject().EnumArgumentError("stack", new[] { WireStack.UGUI, WireStack.SDK });
            string subset = new JObject().EnumArgumentError("stack", new[] { WireStack.UGUI });

            Assert.That(whole, Does.Contain("sdk"));
            Assert.That(subset, Does.Contain("ugui"));
            Assert.That(subset, Does.Not.Contain("sdk"));
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
