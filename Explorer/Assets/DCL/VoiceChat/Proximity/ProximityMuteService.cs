using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.VoiceChat.MutePersistence;
using System;
using System.Threading;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Facade over <see cref="IProximityMuteCache"/> and <see cref="IProximityMuteRepository"/>.
    /// Shared between <see cref="ProximityVoiceChatManager"/> (subscribes to <see cref="MuteStateChanged"/>)
    /// and the user profile context menu (calls <see cref="SetMutedAsync"/>).
    /// </summary>
    public class ProximityMuteService
    {
        private const string TAG = nameof(ProximityMuteService);

        private readonly IProximityMuteCache cache;
        private readonly IProximityMuteRepository? repository;

        /// <summary>
        /// Raised when a participant's mute state changes.
        /// Parameters: walletId, isMuted.
        /// </summary>
        public event Action<string, bool>? MuteStateChanged
        {
            add => cache.MuteStateChanged += value;
            remove => cache.MuteStateChanged -= value;
        }

        public ProximityMuteService(IProximityMuteCache cache, IProximityMuteRepository? repository = null)
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
            if (repository == null) return;

            try
            {
                var mutedUsers = await repository.GetAllMutedUsersAsync(ct);
                cache.Reset(mutedUsers);
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{TAG} Loaded {mutedUsers.Count} muted users");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to load muted users: {ex.Message}");
            }
        }

        /// <summary>
        /// Mutes or unmutes a user, persisting the change to the API.
        /// On API failure, the local cache is not updated.
        /// </summary>
        public async UniTask SetMutedAsync(string walletId, bool muted, CancellationToken ct)
        {
            if (repository != null)
            {
                try
                {
                    if (muted)
                        await repository.MuteUserAsync(walletId, ct);
                    else
                        await repository.UnmuteUserAsync(walletId, ct);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    ReportHub.LogWarning(ReportCategory.VOICE_CHAT, $"{TAG} Failed to {(muted ? "mute" : "unmute")} {walletId} via API, applying locally: {ex.Message}");
                }
            }

            cache.SetMuted(walletId, muted);
        }

        /// <summary>
        /// Synchronous mute for backward compatibility (local only, no API call).
        /// Prefer <see cref="SetMutedAsync"/> when possible.
        /// </summary>
        public void SetMuted(string walletId, bool muted)
        {
            cache.SetMuted(walletId, muted);
        }

        public void ToggleMute(string walletId)
        {
            bool muted = !IsMuted(walletId);
            cache.SetMuted(walletId, muted);
        }
    }
}
