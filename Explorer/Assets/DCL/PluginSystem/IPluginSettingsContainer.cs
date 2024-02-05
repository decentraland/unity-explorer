namespace DCL.PluginSystem
{
    /// <summary>
    ///     Stores settings for plugins
    /// </summary>
    public interface IPluginSettingsContainer
    {
        /// <summary>
        ///     Get a typed settings object or throw an exception if it doesn't exist
        /// </summary>
        T GetSettings<T>() where T: IDCLPluginSettings;
    }
}
