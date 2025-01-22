
using DCL.Clipboard;
using DCL.Input;

namespace MVC
{
    /// <summary>
    /// A set of references to the only systems a view can use directly, without the need of a controller.
    /// </summary>
    public class ViewDependencies
    {
        public DCLInput DclInput;
        public IEventSystem EventSystem;
        public IMVCManager MvcManager;
        public IClipboardManager ClipboardManager;
    }
}
