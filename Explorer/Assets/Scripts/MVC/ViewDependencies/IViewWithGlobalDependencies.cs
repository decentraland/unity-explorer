namespace MVC
{
    /// <summary>
    ///     A view with access to some external systems without needing a controller.
    ///     These dependencies must not be able to change the state of the game in any meaningful way.
    /// </summary>
    public interface IViewWithGlobalDependencies
    {
        /// <summary>
        /// Sets the references to the systems the view may need.
        /// </summary>
        /// <param name="dependencies">References to systems the view may need. Not all are required.</param>
        public void InjectDependencies(ViewDependencies dependencies);
    }
}
