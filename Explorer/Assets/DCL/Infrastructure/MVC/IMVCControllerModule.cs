namespace MVC
{
    public interface IMVCControllerModule
    {
        /// <summary>
        ///     Called after the view is shown
        /// </summary>
        internal void OnViewShow() { }

        /// <summary>
        ///     Called before all other logic connected to view closing
        /// </summary>
        internal void OnViewHide() { }

        internal void OnBlur() { }

        internal void OnFocus() { }
    }
}
