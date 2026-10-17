using Arch.SystemGroups;
using System;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Global world plugin
    /// </summary>
    public interface IDCLGlobalPlugin<in TSettings> : IDCLGlobalPlugin, IDCLPlugin<TSettings> where TSettings: IDCLPluginSettings, new() { }

    public interface IDCLGlobalPlugin : IDCLPlugin
    {
        /// <summary>
        ///     Create dependencies which require the global world and additional arguments
        /// </summary>
        void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments);
    }
}
