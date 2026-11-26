using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Backpack.Tests
{
    public class BackpackSelectWearableShould
    {
        private const string SHIRT = "urn:decentraland:off-chain:base-avatars:blue_tshirt";
        private readonly BackpackCommandBus commands = new ();
        private IBackpackEventBus events = null!;
        private IWearablesProvider provider = null!;
        private IWearable shirt = null!;
        private BackpackBusController handler = null!;

        [SetUp]
        public void SetUp()
        {
            events = Substitute.For<IBackpackEventBus>();
            provider = Substitute.For<IWearablesProvider>();
            shirt = Substitute.For<IWearable>();
            shirt.Model.Returns(new StreamableLoadingResult<WearableDTO>(new WearableDTO()));
            shirt.GetUrn().Returns((URN)SHIRT);
            var body = Substitute.For<IWearable>();
            body.GetUrn().Returns(BodyShape.FEMALE);
            var equipped = Substitute.For<IEquippedWearables>();
            equipped.Wearable(WearableCategories.Categories.BODY_SHAPE).Returns(body);
            handler = new BackpackBusController(Substitute.For<IWearableStorage>(), events, commands, equipped, null!, null!, provider, null!, null!);
        }

        [TearDown]
        public void TearDown() => handler.Dispose();

        [Test]
        public void SelectAvailableMetadataAfterAnEmptyResult()
        {
            provider.GetDTOsByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), BodyShape.FEMALE, Arg.Any<CancellationToken>())
                    .Returns(UniTask.FromResult<IReadOnlyCollection<IWearable>?>(Array.Empty<IWearable>()),
                         UniTask.FromResult<IReadOnlyCollection<IWearable>?>(new[] { shirt }));

            commands.SendCommand(new BackpackSelectWearableCommand(SHIRT, default));
            events.DidNotReceiveWithAnyArgs().SendWearableSelect(default!);
            commands.SendCommand(new BackpackSelectWearableCommand(SHIRT.ToUpperInvariant(), default));
            events.Received(1).SendWearableSelect(shirt);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IgnoreLateMetadataAfterLeavingThePageOrSelectingAnotherCard(bool leavePage)
        {
            using var page = new CancellationTokenSource();
            var first = new UniTaskCompletionSource<IReadOnlyCollection<IWearable>?>();
            provider.GetDTOsByPointersAsync(Arg.Any<IReadOnlyCollection<URN>>(), BodyShape.FEMALE, Arg.Any<CancellationToken>())
                    .Returns(first.Task, UniTask.FromResult<IReadOnlyCollection<IWearable>?>(Array.Empty<IWearable>()));

            commands.SendCommand(new BackpackSelectWearableCommand(SHIRT, page.Token));
            if (leavePage)
                page.Cancel();
            else
                commands.SendCommand(new BackpackSelectWearableCommand("urn:decentraland:off-chain:base-avatars:hat", page.Token));

            first.TrySetResult(new[] { shirt });
            events.DidNotReceiveWithAnyArgs().SendWearableSelect(default!);
        }
    }
}
