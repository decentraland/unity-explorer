using UnityEngine;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Simulation lifecycle: tick, draw, and spawn rect configuration.
    /// Consumed by the presenter that drives the update loop.
    /// The service's IDisposable lifetime is managed by the plugin scope, not the presenter.
    /// </summary>
    public interface ISituationalReactionSimulation
    {
        bool WorldReactionsEnabled { get; set; }
        bool ShowRemoteUIReactions { get; set; }
        void SetDefaultUISpawnRect(RectTransform rect);
        void Tick(float dt);
        void Draw(Camera cam);
    }
}
