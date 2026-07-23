using DCL.UI;
using Decentraland.Kernel.Apis;
using System;

namespace DCL.CrdtEcsBridge.JsModulesImplementation
{
    public interface IExplorerUiActions : IDisposable
    {
        OpenExplorerUiResult OpenSection(ExploreSections section);
    }
}
