using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Profiles.Self;
using System.Text.RegularExpressions;
using System.Threading;

namespace DCL.Chat.History
{
    /// <summary>
    /// Builds chat messages according to some parameters and the data provided by the profile repository.
    /// </summary>
    public class ChatMessageFactory
    {
        private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)@([A-Za-z0-9]{3,15}(?:#[A-Za-z0-9]{4})?)(?=\s|!|\?|\.|,|$)", RegexOptions.Compiled);

        private readonly ISelfProfile selfProfile;
        private readonly IProfileRepository profileRepository;

        public ChatMessageFactory(ISelfProfile selfProfile, IProfileRepository profileRepository)
        {
            this.selfProfile = selfProfile;
            this.profileRepository = profileRepository;
        }

        /// <summary>
        /// Generates a new chat message filled with the data provided in the parameters and also by the profile repository.
        /// </summary>
        /// <param name="senderWalletAddress">The wallet address of the user that sent the message.</param>
        /// <param name="isSentByLocalUser">Whether the user that sent the message corresponds to the local user.</param>
        /// <param name="message">The formatted text message.</param>
        /// <param name="usernameOverride">Optional. A sender's username to use instead of the one stored in the profile currently.
        /// Leave it null to use the one provided by the profile.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The task of the asynchronous operation.</returns>
        public async UniTask<ChatMessage> CreateChatMessageAsync(string senderWalletAddress, bool isSentByLocalUser, string message, string usernameOverride, CancellationToken ct)
        {
            Profile ownProfile = await selfProfile.ProfileAsync(ct);

            if (isSentByLocalUser)
            {
                if(string.IsNullOrEmpty(usernameOverride))
                    usernameOverride = ownProfile?.ValidatedName ?? string.Empty;

                return new ChatMessage(
                    message,
                    usernameOverride,
                    senderWalletAddress,
                    true,
                    ownProfile?.WalletId,
                    isMention: false
                );
            }
            else
            {
                Profile profile = await profileRepository.GetAsync(senderWalletAddress, ct);

                if(string.IsNullOrEmpty(usernameOverride))
                    usernameOverride = profile?.ValidatedName ?? string.Empty;

                bool isMention = false;

                if (ownProfile != null)
                    isMention = IsMention(message, ownProfile.MentionName);

                return new ChatMessage(
                    message,
                    usernameOverride,
                    senderWalletAddress,
                    false,
                    profile?.WalletId,
                    isMention,
                    false
                );
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
