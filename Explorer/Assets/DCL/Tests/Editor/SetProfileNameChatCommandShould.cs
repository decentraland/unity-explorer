using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Profiles;
using DCL.Profiles.Self;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System.Threading;

namespace DCL.Tests.Editor
{
    /// <summary>
    ///     The command exists to put a name on a real profile exactly as a crafted one would arrive from the
    ///     catalyst, so what matters is that the name it publishes is byte-for-byte what the tester asked for —
    ///     the moment it starts filtering, it stops reproducing the thing under test.
    /// </summary>
    public class SetProfileNameChatCommandShould
    {
        private const string BACKSLASH = "\\";

        private ISelfProfile selfProfile = null!;
        private Profile profile = null!;
        private SetProfileNameChatCommand command = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Assigning Profile.Name derives the validated name, which reads the features registry.
            EcsTestsUtils.TearDownFeaturesRegistry();
            EcsTestsUtils.SetUpFeaturesRegistry();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() =>
            EcsTestsUtils.TearDownFeaturesRegistry();

        [SetUp]
        public void SetUp()
        {
            profile = new Profile(UserId.New("0x79fdd6f8ba257bda1d5a2a413ae0b43ec300ed10").Unwrap(), "Guybrush", new Avatar());

            selfProfile = Substitute.For<ISelfProfile>();
            selfProfile.ProfileAsync(Arg.Any<CancellationToken>()).Returns(UniTask.FromResult<Profile?>(profile));
            selfProfile.UpdateProfileAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
                       .Returns(call => UniTask.FromResult<Profile?>(call.Arg<Profile>()));

            command = new SetProfileNameChatCommand(selfProfile);
        }

        [Test]
        public void PublishMarkupWithoutEscapingIt()
        {
            // Act — the payload a crafted profile would carry.
            Run("<size=400%><color=#00FF00>Verified", "Admin");

            // Assert — the point of the command: what reaches the catalyst is the raw thing, brackets intact.
            Assert.AreEqual("<size=400%><color=#00FF00>Verified Admin", profile.Name);
        }

        [Test]
        public void RejoinANameSplitOnSpaces()
        {
            // Arrange — the dispatcher hands the command one parameter per space-separated token.
            // Act
            Run("Guild", "of", "the", "Rising", "Sun");

            // Assert
            Assert.AreEqual("Guild of the Rising Sun", profile.Name);
        }

        [Test]
        public void DecodeAnEscapeIntoTheCharacterItDenotes()
        {
            // Arrange — lets a tester set characters the chat input will not carry, and reproduce the very
            // sequences TMP decodes on its own.
            // Act
            Run($"{BACKSLASH}u003Csize=400%{BACKSLASH}u003Ehidden");

            // Assert
            Assert.AreEqual("<size=400%>hidden", profile.Name);
        }

        [Test]
        public void LeaveAMalformedEscapeAsTyped()
        {
            // Act — not four hex digits, so it is part of the name rather than an escape.
            Run($"{BACKSLASH}uZZZZ and {BACKSLASH}u00");

            // Assert
            Assert.AreEqual($"{BACKSLASH}uZZZZ and {BACKSLASH}u00", profile.Name);
        }

        [Test]
        public void RestoreThePreviousNameWhenPublishingFails()
        {
            // Arrange
            selfProfile.UpdateProfileAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
                       .Returns(UniTask.FromResult<Profile?>(null));

            // Act
            string reply = Run("<size=400%>rejected");

            // Assert — a rejected publish must not leave the client displaying a name the catalyst never took.
            Assert.AreEqual("Guybrush", profile.Name);
            StringAssert.StartsWith("🔴", reply);
        }

        [Test]
        public void NotEchoTheRawNameBackIntoItsOwnReply()
        {
            // Act
            string reply = Run("<size=400%>huge");

            // Assert — the reply is emitted as a system message, the one path that is deliberately unsanitized,
            // so echoing the name raw would inject it into the client's own copy.
            Assert.That(reply, Does.Not.Contain("<size=400%>"));
            StringAssert.Contains("size=400%", reply);
        }

        [Test]
        public void BeDebugOnly()
        {
            // Assert — a testing instrument, not a feature; it must not appear in a retail command list.
            Assert.IsTrue(command.DebugOnly);
            Assert.IsFalse(command.ValidateParameters(new string[0]));
            Assert.IsTrue(command.ValidateParameters(new[] { "name" }));
        }

        private string Run(params string[] parameters) =>
            command.ExecuteCommandAsync(parameters, CancellationToken.None).GetAwaiter().GetResult();
    }
}
