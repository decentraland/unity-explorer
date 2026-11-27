using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.Backpack.BackpackBus;
using DCL.UI;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Tests.Editor
{
    public class BackpackEmoteSelectionShould
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void SettleAnUncachedCardWhenMetadataCompletes(bool leaveEmotes, bool missingMetadata)
        {
            const string ITEM = "urn:decentraland:matic:collections-v2:0x123:1";
            IEmoteProvider provider = Substitute.For<IEmoteProvider>();
            IEmote emote = new Emote();
            var ready = new UniTaskCompletionSource();
            provider.GetMetadataByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<CancellationToken>(), Arg.Any<List<IEmote>>())
                    .Returns(call =>
                     {
                         Assert.That(call.Arg<IReadOnlyCollection<URN>>(), Is.EquivalentTo(new URN[] { ITEM }));
                         if (!missingMetadata)
                             call.Arg<List<IEmote>>().Add(emote);

                         return ready.Task;
                     });

            var commands = new BackpackCommandBus();
            var events = new BackpackEventBus();
            using var controller = new BackpackBusController(null!, events, commands, null!, null!, new MemoryEmotesStorage(), null!, provider, null!);
            IEmote? selected = null;
            int ended = 0;
            events.SelectEmoteEvent += item => selected = item;

            commands.SendCommand(new BackpackSelectEmoteCommand(ITEM, () => ended++));
            Assert.That(ended, Is.Zero);
            if (leaveEmotes)
                events.SendChangedBackpackSectionEvent(BackpackSections.Avatar);

            ready.TrySetResult();
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(selected, Is.SameAs(leaveEmotes || missingMetadata ? null : emote));
        }
    }
}