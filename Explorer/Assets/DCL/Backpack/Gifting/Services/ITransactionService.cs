namespace DCL.Backpack.Gifting.Services
{
    /// <summary>
    ///     Responsibility: To be the single point of contact for initiating and monitoring blockchain transactions from within
    ///     the client.
    ///     Method: UniTask
    ///     <TransactionResult>
    ///         SendGiftAsync(string recipientAddress, URN itemUrn, CancellationToken ct).
    ///         Implementation (TransactionService.cs): This will contain the currently-unknown logic for how to trigger a
    ///         transfer. It will likely involve making a signed web request to a catalyst or lambda that prepares the
    ///         transaction and returns a URL for signing.
    /// </summary>
    public interface ITransactionService
    {
    }
}