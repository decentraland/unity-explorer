using CrdtEcsBridge.JsModulesImplementation.Communications;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.PortableExperiences;
using DCL.Multiplayer.Connections.Rooms.Connective;
using ECS;
using SceneRunner.Scene;
using System;
using System.Threading;

namespace SceneRunner
{
    public class PortableExperienceSceneFacade : SceneFacade
    {
        private readonly PortableExperienceRoomFactory roomFactory;
        private readonly ISceneCommunicationPipe sceneCommunicationPipe;

        private IConnectiveRoom? authoritativeRoom;
        private IMessagePipe? authoritativeRoomPipe;
        private string authoritativeSceneId = string.Empty;

        /// <summary>
        ///     True when this experience's authoritative scene room exists and its LiveKit connection is established.
        ///     Drives <c>RealmInfo.isConnectedSceneRoom</c> so the SDK initiates the CRDT handshake for the PX.
        /// </summary>
        public bool IsConnectedSceneRoom =>
            authoritativeRoom != null
            && authoritativeRoom.CurrentState() == IConnectiveRoom.State.Running
            && authoritativeRoom.Room().Info.ConnectionState == LKConnectionState.ConnConnected;

        public PortableExperienceSceneFacade(
            ISceneData sceneData,
            SceneInstanceDependencies.WithRuntimeAndJsAPIBase deps,
            PortableExperienceRoomFactory roomFactory,
            ISceneCommunicationPipe sceneCommunicationPipe) : base(sceneData, deps)
        {
            this.roomFactory = roomFactory;
            this.sceneCommunicationPipe = sceneCommunicationPipe;
            SetIsCurrent(true);
        }

        /// <summary>
        ///     Connects this PX to its world scene room (<c>worlds/{realm}/scenes/{sceneId}/comms</c>) and routes the
        ///     scene's CRDT through it. Awaited as part of the scene loading flow: joining the room is what makes the
        ///     world-content-server spawn the authoritative server, so the PX is not considered loaded until connected.
        /// </summary>
        public async UniTask StartAuthoritativeRoomAsync(IRealmData portableExperienceRealm, CancellationToken ct)
        {
            string? id = SceneData.SceneEntityDefinition.id;
            if (string.IsNullOrEmpty(id)) return;

            authoritativeSceneId = id;
            (authoritativeRoom, authoritativeRoomPipe) = roomFactory.Create(portableExperienceRealm, authoritativeSceneId);
            sceneCommunicationPipe.RegisterSceneRoom(authoritativeSceneId, authoritativeRoom, authoritativeRoomPipe);

            ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, $"Portable Experience comms: connecting scene '{authoritativeSceneId}' of world '{portableExperienceRealm.RealmName}'");
            await authoritativeRoom.StartAsync().AttachExternalCancellation(ct);
        }

        protected override void DisposeInternal()
        {
            if (authoritativeRoom != null)
            {
                sceneCommunicationPipe.RemoveSceneRoom(authoritativeSceneId);
                authoritativeRoomPipe?.Dispose();
                StopAndDisposeAsync(authoritativeRoom).Forget();
                authoritativeRoom = null;
                authoritativeRoomPipe = null;
            }

            base.DisposeInternal();
        }

        private static async UniTaskVoid StopAndDisposeAsync(IConnectiveRoom room)
        {
            try { await room.StopIfNotAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.COMMS_SCENE_HANDLER)); }
            finally { room.Dispose(); }
        }
    }
}
