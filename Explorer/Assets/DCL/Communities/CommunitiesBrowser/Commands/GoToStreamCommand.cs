using DCL.Chat;

namespace DCL.Communities.CommunitiesBrowser.Commands
{
    public class GoToStreamCommand
    {
        /// <summary>
        /// Focuses the Currently Active Community Voice Chat
        /// </summary>
        /// <param name="communityId">The community ID to join</param>
        public void Execute(string communityId)
        {
            ChatOpener.Instance.OpenCommunityConversationWithId(communityId);
        }
    }
}
