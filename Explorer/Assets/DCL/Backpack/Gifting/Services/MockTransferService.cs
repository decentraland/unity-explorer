namespace DCL.Backpack.Gifting.Services
{
    using System;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using Diagnostics;
    using UnityEngine;

    public class MockGiftTransferService : IGiftTransferService
    {
        public event Action<int, DateTime> OnVerificationCodeReceived;

        [Header("Simulation Settings")]
        [Tooltip("The chance of a long delay occurring, from 0.0 (never) to 1.0 (always).")]
        [Range(0f, 1f)]
        public float LongDelayChance { get; set; } = 0.05f;

        [Tooltip("The chance of the transaction failing, from 0.0 (never) to 1.0 (always).")]
        [Range(0f, 1f)]
        public float FailureChance { get; set; } = 0.95f;

        [Header("Delay Durations (in seconds)")]
        [Tooltip("The normal, fast transaction time (must be less than the controller's timeout).")]
        public float ShortDelayDuration { get; set; } = 3.0f;

        [Tooltip("The delayed transaction time (must be more than the controller's timeout).")]
        public float LongDelayDuration { get; set; } = 12.0f;

        public async UniTask<GiftTransferResult> RequestTransferAsync(string fromAddress, string giftUrn, string recipientAddress, CancellationToken ct)
        {
            bool isLongDelay = UnityEngine.Random.value < LongDelayChance;
            bool shouldSucceed = UnityEngine.Random.value > FailureChance;
            float delayDuration = isLongDelay ? LongDelayDuration : ShortDelayDuration;

            ReportHub.Log(ReportCategory.GIFTING, $"[MOCK] Initiating transfer. Simulating a {(isLongDelay ? "LONG" : "SHORT")} delay of {delayDuration}s. Will succeed: {shouldSucceed}.");

            // 1. Simulate receiving a verification code from the backend.
            // This part is always fast.
            await UniTask.Delay(TimeSpan.FromMilliseconds(200), cancellationToken: ct);
            int mockCode = UnityEngine.Random.Range(100000, 999999);
            var mockExpiration = DateTime.UtcNow.AddMinutes(5);
            OnVerificationCodeReceived?.Invoke(mockCode, mockExpiration);

            // 2. Simulate the user signing and the transaction broadcasting.
            // This is where we use our calculated delay.
            ReportHub.Log(ReportCategory.GIFTING, "[MOCK] Simulating user signing and transaction broadcast...");
            await UniTask.Delay(TimeSpan.FromSeconds(delayDuration), cancellationToken: ct);

            // 3. Check for cancellation during the delay.
            if (ct.IsCancellationRequested)
            {
                ReportHub.Log(ReportCategory.GIFTING, "[MOCK] Transfer cancelled by user.");
                return GiftTransferResult.Fail("User cancelled.");
            }

            // 4. Return the configured result.
            if (shouldSucceed)
            {
                ReportHub.Log(ReportCategory.GIFTING, "[MOCK] Transfer successful.");
                return GiftTransferResult.Success();
            }

            ReportHub.Log(ReportCategory.GIFTING, "[MOCK] Transfer failed.");
            return GiftTransferResult.Fail("Mocked backend failure: The transaction was rejected.");
        }
    }
}