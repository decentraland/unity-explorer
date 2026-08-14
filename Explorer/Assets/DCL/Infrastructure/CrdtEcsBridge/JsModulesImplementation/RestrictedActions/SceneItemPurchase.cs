using Cysharp.Threading.Tasks;
using Decentraland.Kernel.Apis;
using System.Threading;

namespace DCL.CrdtEcsBridge.JsModulesImplementation
{
    public interface ISceneItemPurchaseFlow
    {
        UniTask<OpenItemPurchaseResult> OpenAsync(string itemUrn, CancellationToken ct);
    }

    public static class SceneItemPurchaseBridge
    {
        private static ISceneItemPurchaseFlow? flow;


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
