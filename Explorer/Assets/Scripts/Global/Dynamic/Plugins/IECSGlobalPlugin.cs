using Arch.Core;
using Arch.SystemGroups;

namespace Global.Dynamic.Plugins
{
    /// <summary>
    ///     Global world plugin
    /// </summary>
    public interface IECSGlobalPlugin
    {
        void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments);
    }
}
