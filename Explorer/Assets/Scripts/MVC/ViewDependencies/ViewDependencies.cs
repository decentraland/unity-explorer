using DCL.Clipboard;
using DCL.Input;

namespace MVC
{
    /// <summary>
    ///     A set of references to the only systems and managers a view can use directly, without the need of a controller.
    ///     These should not be able to change the state of the game in a meaningful way, but allow for easier access to certain functionalities needed to display data.
    /// </summary>
    public class ViewDependencies
    {
        public readonly DCLInput DclInput;
        public readonly IEventSystem EventSystem;
        public readonly MVCManagerMenusAccessFacade GlobalUIViews;
        public readonly IClipboardManager ClipboardManager;
        public readonly ICursor Cursor;

        public ViewDependencies(DCLInput dclInput, IEventSystem eventSystem, MVCManagerMenusAccessFacade globalUIViews, IClipboardManager clipboardManager, ICursor cursor)
        {
            DclInput = dclInput;
            EventSystem = eventSystem;
            GlobalUIViews = globalUIViews;
            ClipboardManager = clipboardManager;
            Cursor = cursor;
        }

    }
}
