using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.ChatReactions.Simulation.UI;
using DCL.Chat.ChatReactions.Simulation.World;
using UnityEngine;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Drives the per-frame simulation lifecycle for all situational reaction subsystems:
    /// network broadcasting, remote reaction draining, UI particles, and world particles.
    /// </summary>
    internal sealed class SituationalSimulationLoop : ISituationalReactionSimulation
    {
        private readonly ChatReactionUISimulation uiSimulation;
        private readonly ChatReactionWorldSimulation worldSimulation;
        private readonly ReactionNetworkBroadcaster networkBroadcaster;
        private readonly SituationalRemoteTarget remoteTarget;
        private readonly LocalPlayerWorldReactor worldReactor;
        private readonly SituationalReactionFacade facade;
        private readonly StreamReactionsEmitter streamEmitter;

        public bool WorldReactionsEnabled
        {
            get => worldReactor.WorldReactionsEnabled;
            set => worldReactor.WorldReactionsEnabled = value;
        }

        public bool ShowRemoteUIReactions
        {
            get => remoteTarget.ShowRemoteUIReactions;
            set => remoteTarget.ShowRemoteUIReactions = value;
        }

        internal StreamReactionsEmitter StreamEmitter => streamEmitter;

        public SituationalSimulationLoop(
            ChatReactionUISimulation uiSimulation,
            ChatReactionWorldSimulation worldSimulation,
            ReactionNetworkBroadcaster networkBroadcaster,
            SituationalRemoteTarget remoteTarget,
            LocalPlayerWorldReactor worldReactor,
            SituationalReactionFacade facade,
            StreamReactionsEmitter streamEmitter)
        {
            this.uiSimulation = uiSimulation;
            this.worldSimulation = worldSimulation;
            this.networkBroadcaster = networkBroadcaster;
            this.remoteTarget = remoteTarget;
            this.worldReactor = worldReactor;
            this.facade = facade;
            this.streamEmitter = streamEmitter;
        }

        public void SetDefaultUISpawnRect(RectTransform rect) =>
            uiSimulation.SetDefaultSpawnRect(rect);

        public void Tick(float dt)
        {
            facade.TickSendBudget(dt);
            streamEmitter.Tick(dt);
            networkBroadcaster.Tick(dt);
            remoteTarget.Tick(dt);
            uiSimulation.Tick(dt);
            worldSimulation.Tick(dt);
        }

        public void Draw(Camera cam)
        {
            uiSimulation.Draw(cam);
            worldSimulation.Draw(cam);
        }
    }
}
