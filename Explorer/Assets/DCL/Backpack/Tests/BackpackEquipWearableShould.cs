using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Backpack.Tests
{
    public class BackpackEquipWearableShould
    {
        private const string SHIRT = "urn:decentraland:off-chain:base-avatars:blue_tshirt";
        private const int SLOT = 4;
        private readonly List<(int, bool)> busy = new ();
        private readonly List<string> warnings = new ();
        private IWearablesProvider provider = null!;
        private IWearable shirt = null!;
        private AttachmentRegularAsset shirtModel = null!;
        private IWearable sweater = null!;
        private IBackpackEventBus events = null!;
        private BackpackWearableEquip equip = null!;
        private BackpackBusController handler = null!;

        [SetUp]
        public void SetUp()
        {
            busy.Clear();
            warnings.Clear();
            NotificationsBusController.Initialize(new NotificationsBusController());
            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.INTERNAL_SERVER_ERROR,
                notification => warnings.Add(notification.GetHeader()));
            provider = Substitute.For<IWearablesProvider>();
            shirt = Substitute.For<IWearable>();
            shirt.GetUrn().Returns((URN)SHIRT);
            shirt.GetCategory().Returns(WearableCategories.Categories.UPPER_BODY);
            shirtModel = new AttachmentRegularAsset(new GameObject("blue shirt"), AttachmentRegularAsset.RENDERER_INFO_POOL.Get(), Substitute.For<IStreamableRefCountData>());
            var models = new WearableAssets[BodyShape.COUNT];
            models[BodyShape.FEMALE] = new StreamableLoadingResult<AttachmentAssetBase>(shirtModel);
            shirt.WearableAssetResults.Returns(models);
            sweater = Substitute.For<IWearable>();
            var body = Substitute.For<IWearable>();
            body.GetUrn().Returns(BodyShape.FEMALE);
            var equipped = Substitute.For<IEquippedWearables>();
            equipped.Wearable(WearableCategories.Categories.BODY_SHAPE).Returns(body);
            equipped.Wearable(WearableCategories.Categories.UPPER_BODY).Returns(sweater);
            var commands = new BackpackCommandBus();
            events = Substitute.For<IBackpackEventBus>();
            handler = new BackpackBusController(null!, events, commands, equipped, null!, null!, provider, null!, null!);
            equip = new BackpackWearableEquip(provider, equipped, new SmartWearableCache(null!, null!), null!, commands,
                (slot, loading) => busy.Add((slot, loading)));
        }

        [TearDown]
        public void TearDown()
        {
            equip.Cancel();
            handler.Dispose();
            shirtModel.Dispose();
            UnityEngine.Object.DestroyImmediate(shirtModel.MainAsset);
            NotificationsBusController.Reset();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RestoreTheCardAndEquipOnTheNextClick(bool resolverThrows)
        {
            const string FAILURE = "The wearable model could not be loaded";
            if (resolverThrows) LogAssert.Expect(LogType.Exception, new Regex(FAILURE));
            var first = resolverThrows
                ? UniTask.FromException<IWearable?>(new InvalidOperationException(FAILURE))
                : UniTask.FromResult<IWearable?>(null);
            provider.GetForEquipAsync(SHIRT, BodyShape.FEMALE, Arg.Any<CancellationToken>())
                    .Returns(first, UniTask.FromResult<IWearable?>(shirt));

            equip.EquipAsync(SLOT, SHIRT).GetAwaiter().GetResult();
            Assert.That(busy, Is.EqualTo(new[] { (SLOT, true), (SLOT, false) }));
            Assert.That(warnings, Is.EqualTo(new[] { "Couldn't equip this wearable. Try again." }));
            events.DidNotReceiveWithAnyArgs().SendUnEquipWearable(default!);

            equip.EquipAsync(SLOT, SHIRT).GetAwaiter().GetResult();
            events.Received(1).SendUnEquipWearable(sweater);
            events.Received(1).SendEquipWearable(shirt, true);
            Assert.That(warnings.Count, Is.EqualTo(1));
            Assert.That(busy, Is.EqualTo(new[] { (SLOT, true), (SLOT, false), (SLOT, true), (SLOT, false) }));
        }

        [Test]
        public void PreserveTheCurrentTopWhenTheCategoryIsInvalid()
        {
            shirt.GetCategory().Returns(string.Empty);
            provider.GetForEquipAsync(SHIRT, BodyShape.FEMALE, Arg.Any<CancellationToken>())
                    .Returns(UniTask.FromResult<IWearable?>(shirt));
            equip.EquipAsync(SLOT, SHIRT).GetAwaiter().GetResult();
            events.DidNotReceiveWithAnyArgs().SendUnEquipWearable(default!);
            events.DidNotReceiveWithAnyArgs().SendEquipWearable(default!, default);
            Assert.That(warnings.Count, Is.EqualTo(1));
            Assert.That(busy, Is.EqualTo(new[] { (SLOT, true), (SLOT, false) }));
        }

        [Test]
        public void KeepTheModelAliveDuringSmartWearableAuthorization()
        {
            var storage = new WearableStorage();
            storage.AddWearable(SHIRT, shirt, true);
            var budget = Substitute.For<IPerformanceBudget>();
            budget.TrySpendBudget().Returns(true);
            shirt.DTO.Returns(_ =>
            {
                storage.Unload(budget);
                return null;
            });
            provider.GetForEquipAsync(SHIRT, BodyShape.FEMALE, Arg.Any<CancellationToken>())
                    .Returns(UniTask.FromResult<IWearable?>(shirt));

            equip.EquipAsync(SLOT, SHIRT).GetAwaiter().GetResult();

            events.Received(1).SendEquipWearable(shirt, true);
            Assert.That(warnings, Is.Empty);
            Assert.That(shirtModel.ReferenceCount, Is.Zero);
            storage.Unload(budget);
            Assert.That(shirt.WearableAssetResults[BodyShape.FEMALE].Results![0], Is.Null);
        }

        [Test]
        public void PreserveTheCurrentTopIfTheResolvedModelWasEvictedBeforeTheCommand()
        {
            var pending = new UniTaskCompletionSource<IWearable?>();
            provider.GetForEquipAsync(SHIRT, BodyShape.FEMALE, Arg.Any<CancellationToken>()).Returns(pending.Task);
            UniTask attempt = equip.EquipAsync(SLOT, SHIRT);

            shirt.WearableAssetResults[BodyShape.FEMALE] = default;
            pending.TrySetResult(shirt);
            attempt.GetAwaiter().GetResult();

            events.DidNotReceiveWithAnyArgs().SendUnEquipWearable(default!);
            events.DidNotReceiveWithAnyArgs().SendEquipWearable(default!, default);
            Assert.That(warnings.Count, Is.EqualTo(1));
            Assert.That(busy, Is.EqualTo(new[] { (SLOT, true), (SLOT, false) }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IgnoreADuplicateClickAndLateCompletionAfterTheSlotIsReused(bool resolverCancels)
        {
            var first = new UniTaskCompletionSource<IWearable?>();
            var second = new UniTaskCompletionSource<IWearable?>();
            provider.GetForEquipAsync(Arg.Any<URN>(), BodyShape.FEMALE, Arg.Any<CancellationToken>())
                    .Returns(first.Task, second.Task);
            UniTask oldAttempt = equip.EquipAsync(SLOT, SHIRT);
            equip.EquipAsync(SLOT, SHIRT).GetAwaiter().GetResult();
            equip.Cancel();
            UniTask newAttempt = equip.EquipAsync(SLOT, "new-item");

            if (resolverCancels)
                first.TrySetCanceled();
            else
                first.TrySetResult(shirt);
            oldAttempt.GetAwaiter().GetResult();
            Assert.That(busy, Is.EqualTo(new[] { (SLOT, true), (SLOT, false), (SLOT, true) }));
            Assert.That(warnings, Is.Empty);
            events.DidNotReceiveWithAnyArgs().SendEquipWearable(default!, default);
            second.TrySetResult(null);
            newAttempt.GetAwaiter().GetResult();
            Assert.That(busy[busy.Count - 1], Is.EqualTo((SLOT, false)));
        }
    }
}
