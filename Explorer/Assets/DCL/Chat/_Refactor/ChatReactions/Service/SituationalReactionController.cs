using System;
using DCL.Chat.ChatReactions;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.Reactions
{
    /// <summary>
    /// Thin MonoBehaviour driver for the situational reaction particle system.
    /// Handles the Unity lifecycle (Update/LateUpdate) and delegates to the
    /// underlying <see cref="ChatReactionSimulation"/>. All tuning lives in
    /// <see cref="ChatReactionsSituationalConfig"/>.
    /// </summary>
    public sealed class SituationalReactionController : MonoBehaviour, ISituationalReactionService, IDisposable
    {
        [field: SerializeField] public RectTransform LaneRect { get; private set; } = null!;

        private ChatReactionSimulation? simulation;

        public void Initialize(ChatReactionsSituationalConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            simulation?.Dispose();
            simulation = new ChatReactionSimulation(config, LaneRect);
        }

        public void Dispose()
        {
            simulation?.Dispose();
            simulation = null;
        }

        private void Update() =>
            simulation?.Tick(Time.unscaledDeltaTime);

        private void LateUpdate() =>
            simulation?.Draw();

        public void TriggerUIReaction(int emojiIndex, int count) =>
            simulation?.TriggerUIReaction(emojiIndex, count);

        public void TriggerUIReactionFromRect(RectTransform sourceRect, int emojiIndex, int count) =>
            simulation?.TriggerUIReactionFromRect(sourceRect, emojiIndex, count);

        public void TriggerDefaultUIReaction() =>
            simulation?.TriggerDefaultUIReaction();

        public void TriggerDefaultUIReactionFromRect(RectTransform sourceRect) =>
            simulation?.TriggerDefaultUIReactionFromRect(sourceRect);

        public void BeginUIStream(RectTransform sourceRect) =>
            simulation?.BeginUIStream(sourceRect);

        public void EndUIStream() =>
            simulation?.EndUIStream();

        public void ToggleUIStream(RectTransform sourceRect) =>
            simulation?.ToggleUIStream(sourceRect);
    }
}
