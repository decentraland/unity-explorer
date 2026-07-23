using DCL.UI;
using System;

namespace DCL.CrdtEcsBridge.JsModulesImplementation
{
    /// <summary>
    ///     Outcome of an <see cref="IExplorerUiActions.OpenSection" /> request.
    /// </summary>
    public enum OpenSectionResult
    {
        Opened,
        AlreadyOpen,
    }

    public interface IExplorerUiActions : IDisposable
    {
        OpenSectionResult OpenSection(ExploreSections section);
    }
}
