using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.UI;
using Decentraland.Kernel.Apis;
using System.Threading;

namespace DCL.CrdtEcsBridge.JsModulesImplementation
{
    public interface IExplorerUiActions
    {
        /// <summary>
        ///     Opens the explore panel on <paramref name="section" />. <paramref name="ui" /> is the protocol
        ///     value the request came in with: the section is what MVC needs, the protocol value is what the
        ///     scene gets its life cycle events tagged with, and neither maps onto the other.
        ///     <paramref name="requestId" /> is echoed on those events. 0 is the protocol's value for a
        ///     request that asked for no correlation.
        ///     Resolves once the request has been accepted or refused, not when the panel closes.
        /// </summary>
        UniTask<OpenExplorerUiResult> OpenSectionAsync(ExplorerUi ui, ExploreSections section, uint requestId, CancellationToken ct);

        /// <summary>
        ///     Starts the credits purchase of the item <paramref name="itemUrn" /> names. Like
        ///     <see cref="OpenSectionAsync" />, it resolves once the request has been accepted or refused; the
        ///     purchase outcome follows as an event tagged with <paramref name="requestId" />.
        /// </summary>
        UniTask<OpenExplorerUiResult> OpenItemPurchaseAsync(string itemUrn, uint requestId, CancellationToken ct);
    }
}
