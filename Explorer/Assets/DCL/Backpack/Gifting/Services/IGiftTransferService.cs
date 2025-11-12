using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DCL.Backpack.Gifting.Services
{
    public class GiftTransferResult
    {
        public bool IsSuccess { get; }
        public string ErrorMessage { get; }

        public GiftTransferResult(bool isSuccess, string errorMessage = null)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        public static GiftTransferResult Success()
        {
            return new GiftTransferResult (true);
        }

        public static GiftTransferResult Fail(string reason)
        {
            return new GiftTransferResult (false, reason);
        }
    }

    public interface IGiftTransferService
    {
        /// <summary>
        /// Fired when a verification code is available to be shown to the user.
        /// Parameters are: (code, expirationTime)
        /// </summary>
        event Action<int, DateTime> OnVerificationCodeReceived;

        /// <summary>
        ///     Initiates the transfer of a gift by requesting a signature from the user.
        /// </summary>
        UniTask<GiftTransferResult> RequestTransferAsync(string giftUrn, string recipientAddress, CancellationToken ct);
    }
}