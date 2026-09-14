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
        ///     Resolves once the request has been accepted or refused, not when the panel closes.
        /// </summary>
        UniTask<OpenExplorerUiResult> OpenSectionAsync(ExplorerUi ui, ExploreSections section, CancellationToken ct);
    }
}
