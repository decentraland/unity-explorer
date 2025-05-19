using DCL.Profiles;
using DCL.Web3.Identities;
using System.Text.RegularExpressions;

namespace DCL.Chat.History
{
    /// <summary>
    /// Builds chat messages according to some parameters and the data provided by the profile repository.
    /// </summary>
    public class ChatMessageFactory
    {
        private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)@([A-Za-z0-9]{3,15}(?:#[A-Za-z0-9]{4})?)(?=\s|!|\?|\.|,|$)", RegexOptions.Compiled);

        private readonly IProfileCache profileCache;
        private readonly IWeb3IdentityCache web3IdentityCache;

        public ChatMessageFactory(
            IProfileCache profileCache,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.profileCache = profileCache;
            this.web3IdentityCache = web3IdentityCache;
        }

        /// <summary>
        /// Generates a new chat message filled with the data provided in the parameters and also by the profile cache.
        /// Additional note: we need this function to be immediate (not async) to ensure chat messages are propagated in the correct chronological order.
        /// </summary>
        /// <param name="senderWalletAddress">The wallet address of the user that sent the message.</param>
        /// <param name="isSentByLocalUser">Whether the user that sent the message corresponds to the local user.</param>
        /// <param name="message">The formatted text message.</param>
        /// <param name="usernameOverride">Optional. A sender's username to use instead of the one stored in the profile currently.
        /// Leave it null to use the one provided by the profile.</param>
        public ChatMessage CreateChatMessage(string senderWalletAddress, bool isSentByLocalUser, string message, string? usernameOverride)
        {
            Profile? ownProfile = null;

            if (web3IdentityCache.Identity != null)
                ownProfile = profileCache.Get(web3IdentityCache.Identity.Address);

            if (isSentByLocalUser)
            {
                if (string.IsNullOrEmpty(usernameOverride))
                    usernameOverride = ownProfile?.ValidatedName ?? string.Empty;

                return new ChatMessage(
                    message,
                    usernameOverride,
                    senderWalletAddress,
                    true,
                    GetUserHash(ownProfile),
                    isMention: false
                );
            }

            // Using profileCache for immediate access ensures chat messages maintain the correct chronological order
            // since async profile fetching times are unpredictable.
            Profile? profile = profileCache.Get(senderWalletAddress);

            if (string.IsNullOrEmpty(usernameOverride))
                usernameOverride = profile?.ValidatedName ?? string.Empty;

            bool isMention = false;

            if (ownProfile != null)
                isMention = IsMention(message, ownProfile.MentionName);

            return new ChatMessage(
                message,
                usernameOverride,
                senderWalletAddress,
                false,
                GetUserHash(profile),
                isMention,
                false
            );

            string GetUserHash(Profile? profile)
            {
                string userHash;

                if (profile != null)
                    userHash = profile.WalletId ?? string.Empty;
                else
                    userHash = $"#{senderWalletAddress[^4..]}";

                return userHash;
            }
        }

        private bool IsMention(string chatMessage, string userName)
        {
            foreach (Match match in USERNAME_REGEX.Matches(chatMessage))
            {
                if (match.Value == userName)
                    return true;
            }

            return false;
        }
    }
}
