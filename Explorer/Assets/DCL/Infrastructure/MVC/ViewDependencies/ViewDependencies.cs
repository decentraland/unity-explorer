using CodeLess.Attributes;
using DCL.Clipboard;
using DCL.Input;
using DCL.UI.GenericContextMenu.Opener;
using DCL.Web3.Identities;

namespace MVC
{
    /// <summary>
    ///     A set of references to the only systems and managers a view can use directly, without the need of a controller.
    ///     These should not be able to change the state of the game in a meaningful way, but allow for easier access to certain functionalities needed to display data.
    /// </summary>
    [Singleton(SingletonGenerationBehavior.GENERATE_STATIC_ACCESSORS)]
    public partial class ViewDependencies
    {
        internal IEventSystem eventSystem { get; }
        internal IMVCManagerMenusAccessFacade globalUIViews { get; }
        internal ClipboardManager clipboardManager { get; }
        internal ICursor cursor { get; }
        internal IContextMenuOpener contextMenuOpener { get; }
        private IWeb3IdentityCache web3IdentityCache { get; }

        public static IWeb3Identity? CurrentIdentity => Instance.web3IdentityCache.Identity;

        public ViewDependencies(IEventSystem eventSystem, IMVCManagerMenusAccessFacade globalUIViews, ClipboardManager clipboardManager, ICursor cursor,
            IContextMenuOpener contextMenuOpener, IWeb3IdentityCache web3IdentityCache)
        {
            this.eventSystem = eventSystem;
            this.globalUIViews = globalUIViews;
            this.clipboardManager = clipboardManager;
            this.cursor = cursor;
            this.contextMenuOpener = contextMenuOpener;
            this.web3IdentityCache = web3IdentityCache;
        }
    }
}
