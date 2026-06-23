using Arch.SystemGroups;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using DCL.Multiplayer.Connections.PortableExperiences;

namespace DCL.PluginSystem.Global.PortableExperienceComms
{
    /// <summary>
    ///     Global plugin that injects <see cref="PortableExperienceWorldCommsSystem" /> so running authoritative Portable
    ///     Experiences connect to their world scene room (which makes the world-content-server spawn the authoritative
    ///     server) and exchange CRDT with it over that room. The underlying connections are owned and disposed by
    ///     <c>CommsContainer</c> via <see cref="PortableExperienceWorldComms" />.
    /// </summary>
    public class PortableExperienceCommsPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly PortableExperienceWorldComms worldComms;
        private readonly SceneCommunicationPipe sceneCommunicationPipe;

        public PortableExperienceCommsPlugin(PortableExperienceWorldComms worldComms, SceneCommunicationPipe sceneCommunicationPipe)
        {
            this.worldComms = worldComms;
            this.sceneCommunicationPipe = sceneCommunicationPipe;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            PortableExperienceWorldCommsSystem.InjectToWorld(ref builder, worldComms, sceneCommunicationPipe);
        }
    }
}
