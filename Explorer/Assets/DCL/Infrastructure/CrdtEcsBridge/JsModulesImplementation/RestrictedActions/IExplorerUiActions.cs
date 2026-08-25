using DCL.ECSComponents;
using DCL.UI;
using Decentraland.Kernel.Apis;

namespace DCL.CrdtEcsBridge.JsModulesImplementation
{
    public interface IExplorerUiActions
    {
        /// <summary>
        ///     Opens the explore panel on <paramref name="section" />. <paramref name="ui" /> is the protocol
        ///     value the request came in with: the section is what MVC needs, the protocol value is what the
        ///     scene gets its life cycle events tagged with, and neither maps onto the other.
        /// </summary>
        OpenExplorerUiResult OpenSection(ExplorerUi ui, ExploreSections section);
    }
}
