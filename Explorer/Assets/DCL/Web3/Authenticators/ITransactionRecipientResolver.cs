using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Populates the recipient gate and plain-language display fields of a
    ///     <see cref="TransactionConfirmationRequest" /> for a scene-initiated transfer, so the
    ///     confirmation popup can describe who receives the assets instead of showing raw transaction data.
    /// </summary>
    public interface ITransactionRecipientResolver
    {
        UniTask ResolveAsync(TransactionConfirmationRequest request, CancellationToken ct);
    }
}
