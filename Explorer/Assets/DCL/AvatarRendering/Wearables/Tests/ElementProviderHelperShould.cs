using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace DCL.AvatarRendering.Wearables.Tests
{
    [TestFixture]
    public class ElementProviderHelperShould
    {
        private const string EMOTE_POINTER = "urn:decentraland:matic:collections-v2:0x1234567890123456789012345678901234567890:0";
        private const int PUMP_FRAMES = 60;

        private MemoryEmotesStorage emoteStorage = null!;
        private IEmoteProvider emoteProvider = null!;
        private IReadOnlyEquippedWearables equippedWearables = null!;
        private ReportData reportData;

        [SetUp]
        public void SetUp()
        {
            emoteStorage = new MemoryEmotesStorage();
            emoteProvider = Substitute.For<IEmoteProvider>();
            equippedWearables = Substitute.For<IReadOnlyEquippedWearables>();
            reportData = new ReportData(ReportCategory.EMOTE);
        }

        [Test]
        public async Task NotInvokeCallbackForStoredElementWithFailedDto()
        {
            LogAssert.ignoreFailingMessages = true;

            // The exact state ReportAndFinalizeWithError leaves in storage: failed Model, IsLoading == false, DTO == null.
            IEmote emote = IEmote.NewEmpty();
            emote.ResolvedFailedDTO(new StreamableLoadingResult<EmoteDTO>(reportData, new Exception("entities endpoint failed")));
            emoteStorage.Set(EMOTE_POINTER, emote);

            var invocations = 0;
            Fetch(_ => invocations++);
            await PumpAsync();

            Assert.AreEqual(0, invocations, "The callback must not be invoked for a stored element whose DTO load failed");
        }

        [Test]
        public async Task NotInvokeCallbackForStoredElementWithCancelledLoad()
        {
            LogAssert.ignoreFailingMessages = true;

            // The exact state TryFinalizeIfCancelled leaves in storage: default Model, IsLoading == false, DTO == null.
            IEmote emote = IEmote.NewEmpty();
            emote.UpdateLoadingStatus(false);
            emoteStorage.Set(EMOTE_POINTER, emote);

            var invocations = 0;
            Fetch(_ => invocations++);
            await PumpAsync();

            Assert.AreEqual(0, invocations, "The callback must not be invoked for a stored element whose DTO load was cancelled");
        }

        [Test]
        public async Task InvokeCallbackOnceForResolvedStoredElement()
        {
            LogAssert.ignoreFailingMessages = true;

            var emote = new Emote(new StreamableLoadingResult<EmoteDTO>(NewDto(EMOTE_POINTER)), false);
            emoteStorage.Set(EMOTE_POINTER, emote);

            var invocations = 0;
            IEmote? received = null;

            Fetch(e =>
            {
                invocations++;
                received = e;
            });

            await PumpAsync(() => invocations > 0);

            Assert.AreEqual(1, invocations, "The callback must be invoked exactly once for a resolved stored element");
            Assert.AreSame(emote, received);
        }

        [Test]
        public async Task SkipUnresolvedProviderResultsAndInvokeCallbackWithResolvedMatch()
        {
            LogAssert.ignoreFailingMessages = true;

            // Storage miss routes through the provider; a body shape must be equipped on that path.
            var bodyShapeWearable = new Wearable(new StreamableLoadingResult<WearableDTO>(new WearableDTO
            {
                metadata = new WearableDTO.WearableMetadataDto
                {
                    id = BodyShape.MALE,
                    data = new WearableDTO.WearableMetadataDto.DataDto { category = "body_shape" },
                },
            }));

            equippedWearables.Wearable(Arg.Any<string>()).Returns(bodyShapeWearable);

            IEmote unresolved = IEmote.NewEmpty();
            unresolved.UpdateLoadingStatus(false);
            var resolved = new Emote(new StreamableLoadingResult<EmoteDTO>(NewDto(EMOTE_POINTER)), false);

            emoteProvider.GetByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), Arg.Any<BodyShape>(), Arg.Any<CancellationToken>(), Arg.Any<List<IEmote>?>())
                         .Returns(callInfo =>
                          {
                              List<IEmote> results = callInfo.Arg<List<IEmote>>();
                              results.Add(unresolved);
                              results.Add(resolved);
                              return UniTask.FromResult<IReadOnlyCollection<IEmote>?>(results);
                          });

            var invocations = 0;
            IEmote? received = null;

            Fetch(e =>
            {
                invocations++;
                received = e;
            });

            await PumpAsync(() => invocations > 0);

            Assert.AreEqual(1, invocations, "An unresolved provider result must be skipped, not abort the match loop");
            Assert.AreSame(resolved, received);
        }

        private void Fetch(Action<IEmote> onElementFetched) =>
            ElementProviderHelper.FetchElementByPointerAndExecuteAsync<ITrimmedEmote, IEmote, IEmoteProvider.OwnedEmotesRequestOptions, EmoteDTO>(
                                      EMOTE_POINTER, emoteProvider, emoteStorage, equippedWearables, onElementFetched, CancellationToken.None, reportData)
                                 .Forget();

        private static async Task PumpAsync(Func<bool>? until = null)
        {
            for (var i = 0; i < PUMP_FRAMES && (until == null || !until()); i++)
                await UniTask.Yield();
        }

        private static EmoteDTO NewDto(string urn) =>
            new ()
            {
                metadata = new EmoteDTO.EmoteMetadataDto
                {
                    id = urn,
                },
            };
    }
}
