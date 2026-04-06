using System;
using DCL.Chat.ChatReactions.Configs;

namespace DCL.Chat.ChatReactions.Core
{
    /// <summary>
    /// Auto-generates reactions at a configurable rate for performance testing.
    /// Ticked each frame by <see cref="SituationalSimulationLoop"/>.
    /// The facade's send budget gates what reaches the network.
    /// </summary>
    public sealed class StreamReactionsEmitter : IDisposable
    {
        private readonly SituationalReactionFacade facade;
        private readonly ChatReactionsConfig config;
        private float accumulator;

        public bool IsActive { get; private set; }
        public float EmitRate { get; private set; }
        public float SendBudgetRate { get; private set; }

        internal StreamReactionsEmitter(SituationalReactionFacade facade, ChatReactionsConfig config)
        {
            this.facade = facade;
            this.config = config;
        }

        public void Start(float emitRate, float sendBudget)
        {
            EmitRate = emitRate;
            SendBudgetRate = sendBudget;
            accumulator = 0f;
            IsActive = true;
            facade.EnableSendBudget(sendBudget);
        }

        public void StartWithDefaults()
        {
            Start(config.StreamCommandEmitRate, config.StreamCommandSendBudget);
        }

        public void Stop()
        {
            IsActive = false;
            accumulator = 0f;
            facade.DisableSendBudget();
        }

        public void Dispose()
        {
            if (IsActive)
                Stop();
        }

        public void Tick(float dt)
        {
            if (!IsActive)
                return;

            accumulator += dt * EmitRate;

            while (accumulator >= 1f)
            {
                accumulator -= 1f;
                facade.TriggerDefaultUIReaction();
            }
        }
    }
}
