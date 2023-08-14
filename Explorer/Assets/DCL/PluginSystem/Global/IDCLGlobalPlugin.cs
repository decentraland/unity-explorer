using Arch.SystemGroups;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Global world plugin
    /// </summary>
    public interface IDCLGlobalPlugin<in TSettings> : IDCLGlobalPlugin, IDCLPlugin<TSettings> where TSettings: IDCLPluginSettings, new() { }

    public interface IDCLGlobalPlugin : IDCLPlugin
    {
        void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments);
    }
}
