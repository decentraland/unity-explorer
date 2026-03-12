using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.ChatReactions.Configs;
using DCL.Diagnostics;
using DCL.Multiplayer.Profiles.Tables;
using Utility;

namespace DCL.Chat.ChatReactions
{
    public sealed class MockReactionMessageBus : IReactionMessageBus
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ChatReactionsConfig config;
        private readonly int atlasTotalTiles;
        private readonly System.Random rng;
        private readonly CancellationTokenSource cts;

        public event Action<ReactionReceivedArgs>? ReactionReceived;

        public MockReactionMessageBus(
            IReadOnlyEntityParticipantTable entityParticipantTable,
            ChatReactionsConfig config,
            int atlasTotalTiles)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.config = config;
            this.atlasTotalTiles = Math.Max(1, atlasTotalTiles);
            rng = new System.Random();
            cts = new CancellationTokenSource();

            SimulationLoopAsync(cts.Token).Forget();
        }

        public void SendSituationalReaction(int emojiIndex) { }

        public void SendMessageReaction(int emojiIndex, string messageId) { }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
        }

        private async UniTaskVoid SimulationLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    float delay = ResolveRandomInterval();
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
                    await UniTask.SwitchToMainThread(ct);

                    if (!config.MockEnabled)
                        continue;

                    string? walletId = PickRandomNearbyWallet();
                    if (walletId == null) continue;

                    EmitMockReaction(walletId);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES); }
            }
        }

        private float ResolveRandomInterval() =>
            config.WorldLane.MockIntervalMin + (float)(rng.NextDouble() * (config.WorldLane.MockIntervalMax - config.WorldLane.MockIntervalMin));

        private string? PickRandomNearbyWallet()
        {
            IReadOnlyCollection<string> wallets = entityParticipantTable.Wallets();
            if (wallets.Count == 0) return null;

            int walletIndex = rng.Next(0, wallets.Count);
            int idx = 0;

            foreach (string w in wallets)
            {
                if (idx++ == walletIndex)
                    return w;
            }

            return null;
        }

        private void EmitMockReaction(string walletId)
        {
            int emojiIndex = rng.Next(0, atlasTotalTiles);
            int minBurst = config.WorldLane.MockMinEmojisPerBurst;
            int maxBurst = config.WorldLane.MockMaxEmojisPerBurst;
            int count = rng.Next(Math.Min(minBurst, maxBurst), Math.Max(minBurst, maxBurst) + 1);

            ReactionReceived?.Invoke(new ReactionReceivedArgs(
                walletId, emojiIndex, count, ReactionType.Situational, string.Empty));
        }
    }
}
