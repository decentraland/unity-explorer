using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Profiles.Self;
using System.Text.RegularExpressions;
using System.Threading;

namespace DCL.Chat.History
{
    /// <summary>
    ///
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
        ///
        /// </summary>
        /// <param name="senderWalletAddress"></param>
        /// <param name="isSentByLocalUser"></param>
        /// <param name="message"></param>
        /// <param name="currentUsername">Optional.</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async UniTask<ChatMessage> CreateChatMessageAsync(string senderWalletAddress, bool isSentByLocalUser, string message, string currentUsername, CancellationToken ct)
        {
            Profile ownProfile = await selfProfile.ProfileAsync(ct);

            if (isSentByLocalUser)
            {
                if(string.IsNullOrEmpty(currentUsername))
                    currentUsername = ownProfile?.ValidatedName ?? string.Empty;

                return new ChatMessage(
                    message,
                    currentUsername,
                    senderWalletAddress,
                    true,
                    ownProfile?.WalletId,
                    isMention: false
                );
            }
            else
            {
                Profile profile = await profileRepository.GetAsync(senderWalletAddress, ct);

                if(string.IsNullOrEmpty(currentUsername))
                    currentUsername = profile?.ValidatedName ?? string.Empty;

                bool isMention = false;

                if (ownProfile != null)
                    isMention = IsMention(message, ownProfile.MentionName);

                return new ChatMessage(
                    message,
                    currentUsername,
                    senderWalletAddress,
                    false,
                    profile?.WalletId,
                    isMention
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
