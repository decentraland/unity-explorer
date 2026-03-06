using System;
using DCL.Chat.ChatReactions;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;

namespace DCL.Chat.Reactions
{
    public sealed class SituationalReactionService : ISituationalReactionService, IDisposable
    {
        private readonly ChatReactionSimulation simulation;

        public SituationalReactionService(ChatReactionsSituationalConfig config, RectTransform laneRect)
        {
            simulation = new ChatReactionSimulation(config, laneRect);
        }

        public void Dispose() => simulation.Dispose();

        public void Tick(float dt) => simulation.Tick(dt);

        public void Draw(Camera cam) => simulation.Draw(cam);

        public void TriggerUIReaction(int emojiIndex, int count) =>
            simulation.TriggerUIReaction(emojiIndex, count);

        public void TriggerUIReactionFromRect(RectTransform sourceRect, int emojiIndex, int count) =>
            simulation.TriggerUIReactionFromRect(sourceRect, emojiIndex, count);

        public void TriggerDefaultUIReaction() =>
            simulation.TriggerDefaultUIReaction();

        public void TriggerDefaultUIReactionFromRect(RectTransform sourceRect) =>
            simulation.TriggerDefaultUIReactionFromRect(sourceRect);

        public void BeginUIStream(RectTransform sourceRect) =>
            simulation.BeginUIStream(sourceRect);

        public void EndUIStream() =>
            simulation.EndUIStream();

        public void ToggleUIStream(RectTransform sourceRect) =>
            simulation.ToggleUIStream(sourceRect);
    }
}
