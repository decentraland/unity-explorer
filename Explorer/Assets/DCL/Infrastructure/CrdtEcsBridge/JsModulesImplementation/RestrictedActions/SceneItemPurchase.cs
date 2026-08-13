using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.CrdtEcsBridge.JsModulesImplementation
{
    /// <summary>
    ///     Verdict of an OpenItemPurchase request. An enum rather than a bool so new outcomes stay
    ///     expressible, and deliberately coarse: "insufficient credits" is folded into
    ///     <see cref="Failed" /> so scene code cannot probe a wallet's balance by attempting purchases.
    ///     <para>
    ///         Mirrors <c>OpenItemPurchaseResult</c> in decentraland/protocol
    ///         (proto/decentraland/kernel/apis/restricted_actions.proto). It is declared by hand because the
    ///         client pins a protocol build off the <c>experimental</c> branch, and the proto change landed on
    ///         <c>main</c>: regenerating from a main-based build here would revert the experimental-only
    ///         changes to comms/rfc4, avatar_shape and light_source. Replace this with the generated type
    ///         once a protocol bump carries it -- the values are already in the same order.
    ///     </para>
    /// </summary>
    public enum SceneItemPurchaseResult
    {
        Unspecified = 0,
        Purchased = 1,

        /// <summary>The player closed the confirmation without buying.</summary>
        Dismissed = 2,
        RejectedNotCurrentScene = 3,
        RejectedNoUserGesture = 4,

        /// <summary>Feature flags off, wallet not allowed, or no client-side purchase flow registered.</summary>
        RejectedFeatureDisabled = 5,

        /// <summary>No listing for that URN: sold out, never listed, or not a collection item.</summary>
        RejectedNotPurchasable = 6,
        Failed = 7,
    }

    /// <summary>
    ///     The client-side purchase flow, as seen by the scene runtime. Implemented on the credits side.
    /// </summary>
    public interface ISceneItemPurchaseFlow
    {
        /// <summary>
        ///     Resolves the item, runs the client's own confirmation, and reports what happened. Receives the
        ///     URN and nothing else: price resolution, signing and UI all belong to the implementation.
        /// </summary>
        UniTask<SceneItemPurchaseResult> OpenAsync(string itemUrn, CancellationToken ct);
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

        /// <summary>Drops the implementation so a disposed owner is never called back into.</summary>
        public static void Unregister() =>
            flow = null;

        public static UniTask<SceneItemPurchaseResult> OpenAsync(string itemUrn, CancellationToken ct)
        {
            // Nothing registered means the client was built without the purchase flow, or it failed to
            // initialize. Report it as disabled rather than throwing into scene code.
            ISceneItemPurchaseFlow? current = flow;

            return current == null
                ? UniTask.FromResult(SceneItemPurchaseResult.RejectedFeatureDisabled)
                : current.OpenAsync(itemUrn, ct);
        }
    }
}
