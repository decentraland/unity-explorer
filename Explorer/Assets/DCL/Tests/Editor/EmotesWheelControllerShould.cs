using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.DTO;
using DCL.EmotesWheel;
using NSubstitute;
using NUnit.Framework;

namespace DCL.Tests.Editor
{
    public class EmotesWheelControllerShould
    {
        [Test]
        public void WaitWhileTheDefinitionIsStillLoading()
        {
            // Arrange
            IEmote emote = Substitute.For<IEmote>();
            emote.IsLoading.Returns(true);
            emote.DTO.Returns((AvatarAttachmentDTO?)null);

            // Act
            bool shouldWait = EmotesWheelController.ShouldWaitForDefinition(emote);

            // Assert
            Assert.IsTrue(shouldWait);
        }

        [Test]
        public void NotWaitWhenTheResolvedDefinitionStillLoadsItsAssets()
        {
            // Arrange
            IEmote emote = Substitute.For<IEmote>();
            emote.IsLoading.Returns(true);
            emote.DTO.Returns(new EmoteDTO { metadata = new EmoteDTO.EmoteMetadataDto() });

            // Act
            bool shouldWait = EmotesWheelController.ShouldWaitForDefinition(emote);

            // Assert
            Assert.IsFalse(shouldWait);
        }

        [Test]
        public void NotWaitWhenLoadingEndedWithoutADefinition()
        {
            // Arrange
            IEmote emote = Substitute.For<IEmote>();
            emote.IsLoading.Returns(false);
            emote.DTO.Returns((AvatarAttachmentDTO?)null);

            // Act
            bool shouldWait = EmotesWheelController.ShouldWaitForDefinition(emote);

            // Assert
            Assert.IsFalse(shouldWait);
        }
    }
}
