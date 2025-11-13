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
        ///     Initiates the transfer of a gift by requesting a signature from the user.
        /// </summary>
        UniTask<GiftTransferResult> RequestTransferAsync(string fromAddress, string giftUrn, string recipientAddress, CancellationToken ct);
    }
}