using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.PortableExperiences;
using DCL.Multiplayer.Connections.Rooms.Connective;
using ECS;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using System.Collections.Generic;

// Lives in the multiplayer-connections namespace (not DCL.PluginSystem.*) on purpose: the
// DCL.PluginSystem.World namespace would shadow Arch.Core.World, and the Arch query source
// generator emits `World` unqualified into this type's namespace.
namespace DCL.Multiplayer.Connections.PortableExperiences
{
    /// <summary>
    ///     Keeps the comms wiring of each running authoritative Portable Experience scene in sync with the loaded
    ///     experiences. For every authoritative PX scene it (1) ensures a scene-level LiveKit connection exists
    ///     (so the world-content-server spawns the authoritative server) and (2) registers that room with the shared
    ///     <see cref="SceneCommunicationPipe" />, so the scene's CRDT is exchanged with the <c>authoritative-server</c>
    ///     over the PX room instead of the host's current scene room. Worlds whose scene is no longer loaded are
    ///     unregistered and disconnected.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PortableExperienceWorldCommsSystem : BaseUnityLoopSystem
    {
        private readonly PortableExperienceWorldComms worldComms;
        private readonly SceneCommunicationPipe sceneCommunicationPipe;
        private readonly Dictionary<string, IRealmData> realmDataByEns = new ();
        private readonly HashSet<string> liveSceneIds = new ();

        internal PortableExperienceWorldCommsSystem(World world, PortableExperienceWorldComms worldComms, SceneCommunicationPipe sceneCommunicationPipe) : base(world)
        {
            this.worldComms = worldComms;
            this.sceneCommunicationPipe = sceneCommunicationPipe;
        }

        protected override void Update(float t)
        {
            realmDataByEns.Clear();
            liveSceneIds.Clear();

            // The Portable Experience realm entity carries the world's comms data (keyed by ENS); the scene entity
            // carries the authoritativeMultiplayer flag and the sceneId. Collect the realms first, then connect and
            // register the scene rooms of authoritative scenes and reconcile the rest away.
            CollectRealmDataQuery(World);
            ActivateAuthoritativeCommsQuery(World);

            // Stop routing for unloaded scenes before disposing their rooms.
            sceneCommunicationPipe.RetainOnlyRooms(liveSceneIds);
            worldComms.RetainOnly(liveSceneIds);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CollectRealmData(in PortableExperienceComponent portableExperience, in PortableExperienceRealmComponent realm)
        {
            realmDataByEns[portableExperience.Ens.ToString()] = realm.RealmData;
        }

        [Query]
        [None(typeof(PortableExperienceRealmComponent), typeof(DeleteEntityIntention))]
        private void ActivateAuthoritativeComms(in PortableExperienceComponent portableExperience, in SceneDefinitionComponent sceneDefinition)
        {
            if (!sceneDefinition.IsPortableExperience) return;
            if (!sceneDefinition.Definition.metadata.authoritativeMultiplayer) return;

            if (!realmDataByEns.TryGetValue(portableExperience.Ens.ToString(), out IRealmData realmData)) return;

            string sceneId = sceneDefinition.Definition.id;
            if (string.IsNullOrEmpty(sceneId)) return;

            liveSceneIds.Add(sceneId);

            worldComms.EnsureConnected(sceneId, realmData);

            if (worldComms.TryGetRoom(sceneId, out IConnectiveRoom room, out IMessagePipe roomPipe))
                sceneCommunicationPipe.RegisterSceneRoom(sceneId, room, roomPipe);
        }
    }
}
