using DCL.Clipboard;
using DCL.Input;
using DCL.Profiles;
using DCL.UI.Profiles.Helpers;

namespace MVC
{
    /// <summary>
    ///     A set of references to the only systems and managers a view can use directly, without the need of a controller.
    /// </summary>
    public class ViewDependencies
    {
        public readonly DCLInput DclInput;
        public readonly IEventSystem EventSystem;
        public readonly MVCManagerMenusAccessFacade GlobalUIViews;
        public readonly IClipboardManager ClipboardManager;
        public readonly ICursor Cursor;
        public readonly IProfileRepository ProfileRepository;
        public readonly IProfileNameColorHelper ProfileNameColorHelper;

        public ViewDependencies(DCLInput dclInput, IEventSystem eventSystem, MVCManagerMenusAccessFacade globalUIViews, IClipboardManager clipboardManager, ICursor cursor,
            IProfileRepository profileRepository, IProfileNameColorHelper profileNameColorHelper)
        {
            DclInput = dclInput;
            EventSystem = eventSystem;
            GlobalUIViews = globalUIViews;
            ClipboardManager = clipboardManager;
            Cursor = cursor;
            ProfileRepository = profileRepository;
            ProfileNameColorHelper = profileNameColorHelper;
        }
    }
}
