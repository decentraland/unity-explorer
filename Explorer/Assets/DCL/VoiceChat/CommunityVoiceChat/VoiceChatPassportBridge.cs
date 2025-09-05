using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    /// <summary>
    ///     Simple bridge to open passport from voice chat without creating cyclic dependencies.
    ///     This follows the same pattern as other passport bridges in the codebase.
    /// </summary>
    public static class VoiceChatPassportBridge
    {
        /// <summary>
        /// Opens the passport for the specified user ID using the MVC manager from ViewDependencies.
        /// </summary>
        /// <param name="userId">The user ID to open the passport for</param>
        /// <param name="ct">Cancellation token</param>
        public static async UniTask OpenPassportAsync(string userId, CancellationToken ct = default)
        {
            try
            {
                // Use the new direct passport opening method from the facade
                await ViewDependencies.GlobalUIViews.OpenPassportAsync(userId, ct);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to open passport for user {userId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens the passport for the specified user ID without waiting for completion.
        /// </summary>
        /// <param name="userId">The user ID to open the passport for</param>
        public static void OpenPassport(string userId)
        {
            OpenPassportAsync(userId, CancellationToken.None).Forget();
        }
    }
}
