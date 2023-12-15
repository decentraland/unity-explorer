namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Builder used by Plugins to schedule the creation of individual debug widgets
    /// </summary>
    public interface IDebugContainerBuilder
    {
        DebugWidgetBuilder AddWidget(string name);
    }
}
