namespace DCL.PluginSystem
{
    /// <summary>
    ///     Indicates that plugin contains no serializable settings
    /// </summary>
    public class NoExposedPluginSettings : IDCLPluginSettings
    {
        public static NoExposedPluginSettings Instance { get; } = new ();
    }
}
