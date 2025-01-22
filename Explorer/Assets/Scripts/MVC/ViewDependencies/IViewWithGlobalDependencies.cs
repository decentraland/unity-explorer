
namespace MVC
{
    /// <summary>
    /// A view that needs to access some external systems without needing a controller.
    /// </summary>
    public interface IViewWithGlobalDependencies : IView
    {
        /// <summary>
        /// Sets the references to the systems the view may need.
        /// </summary>
        /// <param name="dependencies">References to systems the view may need. Not all are required.</param>
        void InjectDependencies(ViewDependencies dependencies);
    }
}
