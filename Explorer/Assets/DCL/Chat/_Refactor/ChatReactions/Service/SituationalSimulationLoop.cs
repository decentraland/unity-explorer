using UnityEngine;

namespace DCL.Chat.ChatReactions
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

        public SituationalSimulationLoop(
            ChatReactionUISimulation uiSimulation,
            ChatReactionWorldSimulation worldSimulation,
            ReactionNetworkBroadcaster networkBroadcaster,
            SituationalRemoteTarget remoteTarget,
            LocalPlayerWorldReactor worldReactor)
        {
            this.uiSimulation = uiSimulation;
            this.worldSimulation = worldSimulation;
            this.networkBroadcaster = networkBroadcaster;
            this.remoteTarget = remoteTarget;
            this.worldReactor = worldReactor;
        }

        public void SetDefaultUISpawnRect(RectTransform rect) =>
            uiSimulation.SetDefaultSpawnRect(rect);

        public void Tick(float dt)
        {
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
