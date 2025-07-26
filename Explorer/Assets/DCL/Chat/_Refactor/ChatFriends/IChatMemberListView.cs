using System.Collections.Generic;

namespace DCL.Chat
{
    public interface IChatMemberListView
    {
        void Show();
        void Hide();
        void SetData(IReadOnlyList<ChatMemberListView.MemberData> members);
        void SetMemberCount(int memberCount);
    }
}