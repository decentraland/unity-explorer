using DCL.AvatarRendering.Emotes.Load;
using DCL.Diagnostics;
using ECS.StreamableLoading.Common.Components;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class LoadEmotesByPointersSystemShould
    {
        [Test]
        public void RemoveTheEmotesWhoseDTOFailedFromTheResult()
        {
            // Arrange
            var resolved = new Emote(new StreamableLoadingResult<EmoteDTO>(new EmoteDTO
            {
                metadata = new EmoteDTO.EmoteMetadataDto { id = "urn:decentraland:off-chain:base-emotes:wave" },
            }));

            var failed = new Emote
            {
                Model = new StreamableLoadingResult<EmoteDTO>(new ReportData(ReportCategory.EMOTE), new Exception("Emote DTO is null")),
            };

            var emotes = new List<IEmote> { failed, resolved };

            // Act
            LoadEmotesByPointersSystem.RemoveEmotesWithFailedDTO(emotes);

            // Assert
            Assert.That(emotes, Is.EquivalentTo(new IEmote[] { resolved }));
        }
    }
}
