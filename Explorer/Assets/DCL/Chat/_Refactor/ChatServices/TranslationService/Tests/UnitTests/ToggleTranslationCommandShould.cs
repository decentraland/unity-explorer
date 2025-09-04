using NUnit.Framework;
using Moq;
using Utility; // For IEventBus

namespace DCL.Translation.Service.Tests.UnitTests
{
    public class ToggleAutoTranslateCommandShould
    {
        [Test]
        public void FlipTheSettingAndPublishEvent()
        {
            // Arrange
            var settingsMock = new Mock<ITranslationSettings>();
            var eventBusMock = new Mock<IEventBus>();
            var command = new ToggleAutoTranslateCommand(settingsMock.Object, eventBusMock.Object);
            string conversationId = "test-convo";

            // Setup the initial state to be false
            settingsMock.Setup(s => s.GetAutoTranslateForConversation(conversationId)).Returns(false);

            // Act
            command.Execute(conversationId);

            // Assert
            // Verify that the setting was changed to true
            settingsMock.Verify(s => s.SetAutoTranslateForConversation(conversationId, true), Times.Once);
            // Verify that the correct event was published
            eventBusMock.Verify(bus => bus.Publish(It.Is<TranslationEvents.ConversationAutoTranslateToggled>(evt => evt.ConversationId == conversationId && evt.IsEnabled == true)), Times.Once);
        }
    }
}