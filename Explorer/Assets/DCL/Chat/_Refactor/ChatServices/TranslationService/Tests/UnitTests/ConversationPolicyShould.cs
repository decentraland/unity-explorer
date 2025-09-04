using NUnit.Framework;
using DCL.Translation.Models;
using DCL.Translation.Settings;
using DCL.Chat.History;
using NSubstitute;

namespace DCL.Translation.Service.Tests.UnitTests
{

    public class ConversationTranslationPolicyShould
    {
        private Mock<ITranslationSettings> settingsMock;
        private ConversationTranslationPolicy policy;

        [SetUp]
        public void SetUp()
        {
            settingsMock = new Mock<ITranslationSettings>();
            policy = new ConversationTranslationPolicy(settingsMock.Object);
        }

        [Test]
        public void NotTranslateWhenGloballyDisabled()
        {
            // Arrange
            settingsMock.Setup(s => s.IsGloballyEnabled).Returns(false);
            var message = new ChatMessage("id", "a valid message", /*...other params...*/);

            // Act
            bool result = policy.ShouldAutoTranslate(message, "any-channel", LanguageCode.Es);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void NotTranslateWhenUserOptsOut()
        {
            // Arrange
            settingsMock.Setup(s => s.IsGloballyEnabled).Returns(true);
            var message = new ChatMessage("id", "a valid message", /*...other params...*/);

            // Act
            bool result = policy.ShouldAutoTranslate(message, "any-channel", LanguageCode.DontTranslate);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void TranslateWhenAllConditionsAreMet()
        {
            // Arrange
            settingsMock.Setup(s => s.IsGloballyEnabled).Returns(true);
            settingsMock.Setup(s => s.GetAutoTranslateForConversation(It.IsAny<string>())).Returns(true);
            var message = new ChatMessage("id", "this is a perfectly valid and long message for translation", /*...other params...*/);

            // Act
            bool result = policy.ShouldAutoTranslate(message, "any-channel", LanguageCode.Es);

            // Assert
            Assert.IsTrue(result);
        }
    }
}