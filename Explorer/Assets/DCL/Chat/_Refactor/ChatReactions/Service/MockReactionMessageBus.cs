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
        private readonly ChatReactionsWorldLaneConfig config;
        private readonly int atlasTotalTiles;
        private readonly System.Random rng;
        private readonly CancellationTokenSource cts;

        public event Action<ReactionReceivedArgs>? ReactionReceived;

        public MockReactionMessageBus(
            IReadOnlyEntityParticipantTable entityParticipantTable,
            ChatReactionsWorldLaneConfig config,
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
                    float delay = config.MockIntervalMin + (float)(rng.NextDouble() * (config.MockIntervalMax - config.MockIntervalMin));
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
                    await UniTask.SwitchToMainThread(ct);

                    if (!config.MockEnabled)
                        continue;

                    IReadOnlyCollection<string> wallets = entityParticipantTable.Wallets();

                    if (wallets.Count == 0)
                        continue;

                    int walletIndex = rng.Next(0, wallets.Count);

                    string? walletId = null;
                    int idx = 0;

                    foreach (string w in wallets)
                    {
                        if (idx++ == walletIndex) { walletId = w; break; }
                    }

                    if (walletId == null) continue;

                    int emojiIndex = rng.Next(0, atlasTotalTiles);
                    int minBurst = config.MockMinEmojisPerBurst;
                    int maxBurst = config.MockMaxEmojisPerBurst;
                    int count = rng.Next(Math.Min(minBurst, maxBurst), Math.Max(minBurst, maxBurst) + 1);

                    ReactionReceived?.Invoke(new ReactionReceivedArgs(
                        walletId, emojiIndex, count, ReactionType.Situational, string.Empty));
                }
                catch (OperationCanceledException) { break; }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.CHAT_MESSAGES); }
            }
        }
    }
}
