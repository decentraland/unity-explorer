using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.VoiceChat.Nearby.MutePersistence;
using System;
using System.Threading;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    /// Facade over <see cref="INearbyMuteCache"/> and <see cref="INearbyMuteRepository"/>.
    /// Shared between <see cref="NearbyVoiceChatManager"/> (subscribes to <see cref="MuteStateChanged"/>)
    /// and the user profile context menu (calls <see cref="SetMutedAsync"/>).
    /// </summary>
    public class NearbyMuteService
    {
        private const string TAG = nameof(NearbyMuteService);

        private readonly INearbyMuteCache cache;
        private readonly INearbyMuteRepository repository;

        /// <summary>
        /// Parameters: walletId, isMuted.
        /// </summary>
        public event Action<string, bool>? MuteStateChanged
        {
            add => cache.MuteStateChanged += value;
            remove => cache.MuteStateChanged -= value;
        }

        public NearbyMuteService(INearbyMuteCache cache, INearbyMuteRepository repository)
        {
            this.cache = cache;
            this.repository = repository;
        }

        public bool IsMuted(string walletId) =>
            cache.IsMuted(walletId);

        /// <summary>
        /// Loads all muted users from the API into the cache.
        /// Should be called once during initialization.
        /// </summary>
        public async UniTask LoadAsync(CancellationToken ct)
        {
            try
            {
                var mutedUsers = await repository.GetAllMutedUsersAsync(ct);
                cache.Merge(mutedUsers);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Loaded {mutedUsers.Count} muted users");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(new Exception($"{TAG} Failed to load muted users", ex), ReportCategory.VOICE_CHAT);
            }
        }

        /// <summary>
        /// Mutes or unmutes a user, persisting the change to the API.
        /// On API failure, the local cache is still updated (graceful degradation).
        /// </summary>
        public async UniTask SetMutedAsync(string walletId, bool muted, CancellationToken ct)
        {
            cache.SetMuted(walletId, muted);

            try
            {
                if (muted)
                    await repository.MuteUserAsync(walletId, ct);
                else
                    await repository.UnmuteUserAsync(walletId, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(new Exception($"{TAG} Failed to {(muted ? "mute" : "unmute")} {walletId} via API, applying locally", ex), ReportCategory.VOICE_CHAT);
            }
        }
    }
}
