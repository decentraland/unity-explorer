using DCL.Translation.Processors;
using DCL.Translation.Processors.DCL.Translation.Service.Processing;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace DCL.Translation
{
    public class TokenizatorShould
    {
        // Orchestration of tokenization
        // rules into a pipeline
        private sealed class Tokenizer
        {
            private readonly IList<ITokenizationRule> rules;

            public Tokenizer(params ITokenizationRule[] rules)
            {
                this.rules = rules;
            }

            public List<Tok> Run(string input)
            {
                var tokens = new List<Tok>
                {
                    new (0, TokType.Text, input)
                };
                foreach (var r in rules)
                    tokens = r.Process(tokens);
                return tokens;
            }
        }

        // Helper: consider link inner "protected" if it's no longer plain Text.
        private static bool IsProtectedInner(Tok t, string expected)
        {
            return t.Value == expected && t.Type != TokType.Text && t.Type != TokType.Tag;
        }

        // Pipeline order is respected (pure NSubstitute, mocking rules + verifying sequence)
        [Test]
        public void PipelineCallsRulesInOrder()
        {
            var r1 = Substitute.For<ITokenizationRule>();
            var r2 = Substitute.For<ITokenizationRule>();
            var r3 = Substitute.For<ITokenizationRule>();

            // Each rule returns a distinct sentinel so we can track propagation
            r1.Process(Arg.Any<List<Tok>>())
                .Returns(ci => new List<Tok>
                {
                    new (0, TokType.Text, "after-r1")
                });

            r2.Process(Arg.Any<List<Tok>>())
                .Returns(ci =>
                {
                    var prev = (List<Tok>)ci[0];
                    Assert.That(prev[0].Value, Is.EqualTo("after-r1")); // got r1 output
                    return new List<Tok>
                    {
                        new (0, TokType.Text, "after-r2")
                    };
                });

            r3.Process(Arg.Any<List<Tok>>())
                .Returns(ci =>
                {
                    var prev = (List<Tok>)ci[0];
                    Assert.That(prev[0].Value, Is.EqualTo("after-r2")); // got r2 output
                    return new List<Tok>
                    {
                        new (0, TokType.Text, "after-r3")
                    };
                });

            var tokenizer = new Tokenizer(r1, r2, r3);
            var final = tokenizer.Run("input");

            // Verify order using NSubstitute's Received.InOrder
            Received.InOrder(() =>
            {
                r1.Process(Arg.Any<List<Tok>>());
                r2.Process(Arg.Any<List<Tok>>());
                r3.Process(Arg.Any<List<Tok>>());
            });

            Assert.That(final.Count, Is.EqualTo(1));
            Assert.That(final[0].Value, Is.EqualTo("after-r3"));
        }

        // AngleBracketSegmentationRule — real rule behavior
        // but sink mocked to confirm call propagation
        [Test]
        public void AngleBracketSegmentationSplitsTagAndText()
        {
            var seg = new AngleBracketSegmentationRule();
            var sink = Substitute.For<ITokenizationRule>();
            sink.Process(Arg.Any<List<Tok>>()).Returns(ci => (List<Tok>)ci[0]);

            var tokenizer = new Tokenizer(seg, sink);
            var tokens = tokenizer.Run("<#00B2FF><link=world>world.dcl.eth</link></color>");

            // Verify sink got called once with seg output
            sink.Received(1).Process(Arg.Any<List<Tok>>());

            Assert.That(tokens.Count, Is.EqualTo(5), "color, link, inner, </link>, </color>");
            Assert.That(tokens[0].Type, Is.EqualTo(TokType.Tag));
            Assert.That(tokens[1].Type, Is.EqualTo(TokType.Tag));
            Assert.That(tokens[2].Type, Is.EqualTo(TokType.Text));
            Assert.That(tokens[2].Value, Is.EqualTo("world.dcl.eth"));
            Assert.That(tokens[3].Type, Is.EqualTo(TokType.Tag));
            Assert.That(tokens[4].Type, Is.EqualTo(TokType.Tag));
        }

        // LinkProtectionRule — inner of <link=...>…</link>
        // is protected (not plain Text anymore)
        [Test]
        public void LinkProtectionProtectsInnerText()
        {
            var seg  = new AngleBracketSegmentationRule();
            var link = new LinkProtectionRule();

            var tokenizer = new Tokenizer(seg, link);
            var tokens = tokenizer.Run("<#00B2FF><link=profile>@Username#c9a1</link></color>");

            Assert.IsTrue(tokens.Exists(t => IsProtectedInner(t, "@Username#c9a1")),
                "Expected the inner link payload to be non-Text (protected) so MT won't touch it");
        }

        // SplitTextTokensOnEmojiRule — isolate emoji
        // graphemes as Emoji tokens
        [Test]
        public void EmojiSplitIsolatesEmojiGraphemes()
        {
            var seg  = new AngleBracketSegmentationRule();
            var emo  = new SplitTextTokensOnEmojiRule();

            var tokenizer = new Tokenizer(seg, emo);
            var tokens = tokenizer.Run("😾😶 hello 😾😶");

            int emojiCount = 0;
            foreach (var t in tokens)
                if (t.Type == TokType.Emoji)
                    emojiCount++;
            Assert.That(emojiCount, Is.EqualTo(4));
            Assert.IsTrue(tokens.Exists(t => t.Type == TokType.Text && t.Value.Trim() == "hello"));
        }

        // SplitNumericAndDateRule — currency + date + time become
        // Number tokens; link inners remain protected
        [Test]
        public void NumericDateTimeAreSplitAsNumberTokens()
        {
            var seg  = new AngleBracketSegmentationRule();
            var num  = new SplitNumericAndDateRule();

            var tokenizer = new Tokenizer(seg, num);
            var tokens = tokenizer.Run("Mi total es $120.50 en 15/09/2025 a las 14:30.");

            var found = new List<string>();
            foreach (var t in tokens)
                if (t.Type == TokType.Number)
                    found.Add(t.Value);

            CollectionAssert.AreEqual(new[]
            {
                "$120.50", "15/09/2025", "14:30"
            }, found);
        }

        [Test]
        public void NumericInsideLinkRemainsProtectedNotNumber()
        {
            var seg  = new AngleBracketSegmentationRule();
            var link = new LinkProtectionRule();
            var num  = new SplitNumericAndDateRule();

            var tokenizer = new Tokenizer(seg, link, num);
            var tokens = tokenizer.Run("Go here: <#00B2FF><link=scene>100,100</link></color>");

            Assert.IsTrue(tokens.Exists(t => IsProtectedInner(t, "100,100")), "link inner should be protected");
            Assert.IsFalse(tokens.Exists(t => t.Type == TokType.Number && t.Value == "100,100"),
                "inner should not be split as Number because it's protected");
        }


        // SplitSlashCommandsRule — inline /help is Command and DOES NOT swallow trailing prose
        [Test]
        public void InlineSlashCommandIsIsolatedAndDoesNotSwallowText()
        {
            var seg  = new AngleBracketSegmentationRule();
            var cmd  = new SplitSlashCommandsRule();

            var tokenizer = new Tokenizer(seg, cmd);
            var tokens = tokenizer.Run("Type /help for a list of commands");

            int iType  = tokens.FindIndex(t => t.Type == TokType.Text    && t.Value.EndsWith("Type "));
            int iCmd   = tokens.FindIndex(t => t.Type == TokType.Command && t.Value == "/help");
            int iTrail = tokens.FindIndex(t => t.Type == TokType.Text    && t.Value.StartsWith(" for a list"));

            Assert.That(iType,  Is.GreaterThanOrEqualTo(0), "Leading prose should be Text");
            Assert.That(iCmd,   Is.GreaterThanOrEqualTo(0), "/help should be Command");
            Assert.That(iTrail, Is.GreaterThanOrEqualTo(0), "Trailing prose should be Text");
            Assert.IsTrue(iType < iCmd && iCmd < iTrail, "Token order must be preserved");
        }

        [Test]
        public void StandaloneSlashCommandYieldsCommandToken()
        {
            var seg  = new AngleBracketSegmentationRule();
            var cmd  = new SplitSlashCommandsRule();

            var tokenizer = new Tokenizer(seg, cmd);
            var tokens = tokenizer.Run("/goto 100,100");

            Assert.IsTrue(tokens.Exists(t => t.Type == TokType.Command && t.Value.StartsWith("/goto")));
        }

        // Full mixed cases – your complex scenario:
        // links + emoji + prose preserved properly
        [Test]
        public void MixedLinksEmojiAndTextKeepInnersAndEmoji()
        {
            var seg  = new AngleBracketSegmentationRule();
            var link = new LinkProtectionRule();
            var emo  = new SplitTextTokensOnEmojiRule();
            var num  = new SplitNumericAndDateRule();
            var cmd  = new SplitSlashCommandsRule();

            var tokenizer = new Tokenizer(seg, link, emo, num, cmd);

            string input =
                "hello my friend <#00B2FF><link=profile>@Username#5e42</link></color> " +
                "i am looking forward to talk to you 😾😶 go here please " +
                "<#00B2FF><link=world>world.dcl.eth</link></color>";

            var tokens = tokenizer.Run(input);

            Assert.IsTrue(tokens.Exists(t => IsProtectedInner(t, "@Username#5e42")));
            Assert.IsTrue(tokens.Exists(t => IsProtectedInner(t, "world.dcl.eth")));

            int emojiCount = 0;
            foreach (var t in tokens)
                if (t.Type == TokType.Emoji)
                    emojiCount++;
            Assert.That(emojiCount, Is.EqualTo(2));
        }

        [Test]
        public void DoubleProfileLinksProduceTwoProtectedInners()
        {
            var seg  = new AngleBracketSegmentationRule();
            var link = new LinkProtectionRule();

            var tokenizer = new Tokenizer(seg, link);
            var tokens = tokenizer.Run(
                "Hello <#00B2FF><link=profile>@Username#c9a1</link></color>," +
                "<#00B2FF><link=profile>@Username#c9a1</link></color> my friends!"
            );

            int protectedCount = 0;
            foreach (var t in tokens)
                if (IsProtectedInner(t, "@Username#c9a1"))
                    protectedCount++;

            Assert.That(protectedCount, Is.EqualTo(2));
        }

        [Test]
        public void MultipleSlashCommandsAreRecognized()
        {
            var cmd = new SplitSlashCommandsRule();
            var tokenizer = new Tokenizer(cmd);

            string input = "/help and then /goto 100,100";
            var tokens = tokenizer.Run(input);

            Assert.That(tokens.FindAll(t => t.Type == TokType.Command).Count, Is.EqualTo(2));
            Assert.IsTrue(tokens.Any(t => t.Value.StartsWith("/help")));
            Assert.IsTrue(tokens.Any(t => t.Value.StartsWith("/goto")));
        }

        [Test]
        public void IdentityRuleDoesNotModifyTokens()
        {
            var passthrough = Substitute.For<ITokenizationRule>();
            passthrough.Process(Arg.Any<List<Tok>>())
                .Returns(ci => (List<Tok>)ci[0]);

            var tokenizer = new Tokenizer(passthrough);

            var tokens = tokenizer.Run("static text only");

            passthrough.Received(1).Process(Arg.Any<List<Tok>>());
            Assert.That(tokens.Count, Is.EqualTo(1));
            Assert.That(tokens[0].Value, Is.EqualTo("static text only"));
            Assert.That(tokens[0].Type, Is.EqualTo(TokType.Text));
        }

        [Test]
        public void RuleReceivesCorrectTokenInput()
        {
            var rule = Substitute.For<ITokenizationRule>();
            rule.Process(Arg.Any<List<Tok>>())
                .Returns(ci => (List<Tok>)ci[0]);

            var tokenizer = new Tokenizer(rule);

            tokenizer.Run("check exact input");

            rule.Received().Process(Arg.Is<List<Tok>>(tokens =>
                tokens.Count == 1 &&
                tokens[0].Value == "check exact input" &&
                tokens[0].Type == TokType.Text
            ));
        }

        [Test]
        public void SlashCommandInsideLinkIsNotMarkedAsCommand()
        {
            var seg  = new AngleBracketSegmentationRule();
            var link = new LinkProtectionRule();
            var cmd  = new SplitSlashCommandsRule();

            var tokenizer = new Tokenizer(seg, link, cmd);
            var tokens = tokenizer.Run("<#00B2FF><link=profile>/help</link></color>");

            Assert.IsTrue(tokens.Exists(t => IsProtectedInner(t, "/help")), "/help inside link should be protected");
            Assert.IsFalse(tokens.Exists(t => t.Type == TokType.Command), "Command rule should not trigger on protected /help");
        }

        [Test]
        public void EmojiSplitPreservesSurroundingTextTokens()
        {
            var seg = new AngleBracketSegmentationRule();
            var emo = new SplitTextTokensOnEmojiRule();

            var tokenizer = new Tokenizer(seg, emo);
            var tokens = tokenizer.Run("hello😶world😾");

            Assert.IsTrue(tokens.Exists(t => t.Type == TokType.Text && t.Value.Contains("hello")), "hello should remain a Text token");
            Assert.IsTrue(tokens.Exists(t => t.Type == TokType.Text && t.Value.Contains("world")), "world should remain a Text token");

            int emojiCount = tokens.FindAll(t => t.Type == TokType.Emoji).Count;
            Assert.That(emojiCount, Is.EqualTo(2), "Should detect two emoji tokens");
        }

        [Test]
        public void MultipleSlashCommandsAreIsolatedIndividually()
        {
            var seg = new AngleBracketSegmentationRule();
            var cmd = new SplitSlashCommandsRule();

            var tokenizer = new Tokenizer(seg, cmd);
            var tokens = tokenizer.Run("/help /goto /command try these!");

            var cmds = tokens.FindAll(t => t.Type == TokType.Command);
            Assert.That(cmds.Count, Is.EqualTo(3), "Should detect 3 separate slash commands");
            CollectionAssert.AreEqual(new[]
            {
                "/help", "/goto", "/command"
            }, cmds.ConvertAll(t => t.Value));
        }

        [Test]
        public void EmojiAndCurrencyAreDetectedSeparately()
        {
            var seg = new AngleBracketSegmentationRule();
            var num = new SplitNumericAndDateRule();
            var emo = new SplitTextTokensOnEmojiRule();

            var tokenizer = new Tokenizer(seg, num, emo);
            var tokens = tokenizer.Run("You owe me $99.99 😾 now.");

            Assert.IsTrue(tokens.Exists(t => t.Type == TokType.Number && t.Value == "$99.99"), "Currency should be tokenized as Number");
            Assert.IsTrue(tokens.Exists(t => t.Type == TokType.Emoji && t.Value == "😾"), "Emoji should be isolated as Emoji token");
        }

        [Test]
        public void SlashCommandInsideLinkIsNotTreatedAsCommand()
        {
            var seg  = new AngleBracketSegmentationRule();
            var link = new LinkProtectionRule();
            var cmd  = new SplitSlashCommandsRule();

            var tokenizer = new Tokenizer(seg, link, cmd);
            var tokens = tokenizer.Run("<#00B2FF><link=profile>/help</link></color>");

            Assert.IsTrue(tokens.Exists(t => IsProtectedInner(t, "/help")), "Command inside link should be protected");
            Assert.IsFalse(tokens.Exists(t => t.Type == TokType.Command), "No Command token should exist if inside protected tag");
        }

        [Test]
        public void SimpleGreetingWithNoTagsIsSingleTextToken()
        {
            var seg  = new AngleBracketSegmentationRule();
            var tokenizer = new Tokenizer(seg);

            const string input = "Hello! How are you doing today?";
            var tokens = tokenizer.Run(input);

            Assert.That(tokens.Count, Is.EqualTo(1));
            Assert.That(tokens[0].Type, Is.EqualTo(TokType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo(input));
        }
    }
}
