using Cysharp.Threading.Tasks;
using Decentraland.Kernel.Apis;
using System.Threading;

namespace DCL.CrdtEcsBridge.JsModulesImplementation
{
    /// <summary>
    ///     The client-side purchase flow, as seen by the scene runtime. Implemented on the credits side.
    /// </summary>
    public interface ISceneItemPurchaseFlow
    {
        /// <summary>
        ///     Resolves the item, runs the client's own confirmation, and reports what happened. Receives the
        ///     URN and nothing else: price resolution, signing and UI all belong to the implementation.
        /// </summary>
        UniTask<OpenItemPurchaseResult> OpenAsync(string itemUrn, CancellationToken ct);
    }

    /// <summary>
    ///     Connects the scene runtime to the purchase flow without either side referencing the other's
    ///     assembly: both only need this one, and the credits side registers its implementation at startup.
    ///     Static for the same reason <c>CreditsFeatureAccess</c> is -- the flow is a single client-wide
    ///     service whose owner initializes long after the scene runtime is constructed.
    /// </summary>
    public static class SceneItemPurchaseBridge
    {
        private static ISceneItemPurchaseFlow? flow;

        public static bool IsAvailable => flow != null;

        public static void Register(ISceneItemPurchaseFlow purchaseFlow) =>
            flow = purchaseFlow;

        public static void Unregister() =>
            flow = null;

        public static UniTask<OpenItemPurchaseResult> OpenAsync(string itemUrn, CancellationToken ct)
        {
            ISceneItemPurchaseFlow? current = flow;

            return current == null
                ? UniTask.FromResult(OpenItemPurchaseResult.OipRejectedFeatureDisabled)
                : current.OpenAsync(itemUrn, ct);
        }
    }
}
