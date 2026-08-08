using DCL.UI;
using NUnit.Framework;

namespace DCL.Tests.Editor
{
    /// <summary>
    ///     <see cref="RichTextSanitizer"/> is the single escaper for strings other users author, so the
    ///     properties every sink relies on are pinned here rather than re-asserted per sink (SEC-034/050/084).
    /// </summary>
    public class RichTextSanitizerShould
    {
        // Assembled from a lone backslash rather than written inline, so nothing on the way into this file can
        // collapse an escape sequence into the character it denotes before the test ever runs.
        private const string BACKSLASH = "\\";

        [TestCase("<size=400%>huge")]
        [TestCase("<color=#00FF00>Verified Admin</color>")]
        [TestCase("<link=\"https://evil.example.com\">click</link>")]
        [TestCase("<b><i><u>nested")]
        public void NeutralizeMarkup(string value)
        {
            // Act
            string escaped = RichTextSanitizer.Escape(value);

            // Assert — TMP only starts parsing a tag at '<', so removing both brackets is what makes it inert.
            Assert.That(escaped, Does.Not.Contain("<").And.Not.Contain(">"), value);
        }

        [Test]
        public void NeutralizeMarkupSmuggledAsAUtf16EscapeSequence()
        {
            // Arrange — a size tag that carries no bracket of its own, so a brackets-only filter passes it through.
            // TMP then decodes the sequence into the real character inside the array its tag parser reads.
            string value = $"{BACKSLASH}u003Csize=0{BACKSLASH}u003Ehidden";

            // Act
            string escaped = RichTextSanitizer.Escape(value);

            // Assert — no surviving backslash means no sequence left to decode.
            Assert.That(escaped, Does.Not.Contain(BACKSLASH), value);
        }

        [Test]
        public void NeutralizeMarkupSmuggledAsAUtf32EscapeSequence()
        {
            // Arrange — TMP decodes the 10-character UTF-32 form as well as the 6-character UTF-16 one.
            string value = $"{BACKSLASH}U0000003Csize=0{BACKSLASH}U0000003Ehidden";

            // Act & Assert
            Assert.That(RichTextSanitizer.Escape(value), Does.Not.Contain(BACKSLASH), value);
        }

        [Test]
        public void NeutralizeAnEscapedQuoteInAttributePosition()
        {
            // Arrange — decodes into the quote that would close a <link="…"> attribute early.
            string value = $"a{BACKSLASH}u0022 hidden";

            // Act & Assert
            Assert.That(RichTextSanitizer.EscapeAttribute(value), Does.Not.Contain(BACKSLASH), value);
        }

        [Test]
        public void KeepTheReadableTextOfNeutralizedMarkup()
        {
            // Act
            string escaped = RichTextSanitizer.Escape("<size=400%>Verified Admin");

            // Assert — the value is made inert, not censored: the user still sees what was sent.
            StringAssert.Contains("Verified Admin", escaped);
            StringAssert.Contains("size=400%", escaped);
        }

        [Test]
        public void LeaveStraightQuotesAloneInContentPosition()
        {
            // Arrange — prose in an announcement body or a display name. With '<' neutralized there is no
            // tag for a quote to escape from, so mangling it would only cost fidelity.
            const string VALUE = "she said \"hello\" — 5\" wide";

            // Act
            string escaped = RichTextSanitizer.Escape(VALUE);

            // Assert
            Assert.AreEqual(VALUE, escaped);
        }

        [Test]
        public void NeutralizeQuotesInAttributePosition()
        {
            // Arrange — interpolated as <link="{value}">, where a quote closes the attribute early and lets
            // the rest of the value be read as further markup.
            // Act
            string escaped = RichTextSanitizer.EscapeAttribute("a\" onclick=b");

            // Assert
            Assert.That(escaped, Does.Not.Contain("\""));
        }

        [TestCase("")]
        [TestCase(null)]
        public void TreatMissingValuesAsEmpty(string? value)
        {
            // Act & Assert — sinks assign the result straight to a label, so it must never be null.
            Assert.AreEqual(string.Empty, RichTextSanitizer.Escape(value));
            Assert.AreEqual(string.Empty, RichTextSanitizer.EscapeAttribute(value));
            Assert.AreEqual(string.Empty, RichTextSanitizer.EscapeAndTruncate(value, 8));
        }

        [Test]
        public void ReturnTheSameInstanceWhenThereIsNothingToEscape()
        {
            // Arrange
            const string VALUE = "an ordinary display name";

            // Act & Assert — the overwhelmingly common case must not allocate a copy per label assignment.
            Assert.AreSame(VALUE, RichTextSanitizer.Escape(VALUE));
        }

        [Test]
        public void CapOversizedValues()
        {
            // Arrange — a long run of nested tags costs TMP an unbounded layout pass even once inert.
            string value = new ('a', 500);

            // Act
            string truncated = RichTextSanitizer.EscapeAndTruncate(value, 32);

            // Assert
            Assert.Less(truncated.Length, value.Length);
            StringAssert.EndsWith("…", truncated);
        }

        [Test]
        public void LeaveValuesWithinTheCapUntouched()
        {
            // Act
            string result = RichTextSanitizer.EscapeAndTruncate("short name", 32);

            // Assert — no stray ellipsis on names that fit.
            Assert.AreEqual("short name", result);
        }

        [Test]
        public void CapWithoutEscapingWhenTheLabelIsNotRichText()
        {
            // Arrange — an announcement body on a label whose richText is off in its prefab.
            const string PROSE = "5 < 10 && 10 > 5";

            // Act
            string result = RichTextSanitizer.Truncate(PROSE, 64);

            // Assert — nothing parses markup there, so the author's brackets must survive verbatim.
            Assert.AreSame(PROSE, result);
        }

        [Test]
        public void CapOversizedValuesWithoutEscaping()
        {
            // Act
            string truncated = RichTextSanitizer.Truncate(new string('a', 500), 32);

            // Assert
            Assert.AreEqual(33, truncated.Length);
            StringAssert.EndsWith("…", truncated);
        }

        [Test]
        public void NeutralizeMarkupThatSurvivesTruncation()
        {
            // Arrange — the cap must not be a way to smuggle a tag past the escaper.
            // Act
            string truncated = RichTextSanitizer.EscapeAndTruncate("<size=400%>" + new string('a', 500), 32);

            // Assert
            Assert.That(truncated, Does.Not.Contain("<").And.Not.Contain(">"));
        }
    }
}
