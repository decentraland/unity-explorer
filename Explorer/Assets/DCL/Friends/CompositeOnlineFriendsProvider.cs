using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connectivity;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Friends
{
    public class CompositeOnlineFriendsProvider : IOnlineUsersProvider
    {
        private readonly IOnlineUsersProvider apiUsersProvider;
        private readonly IOnlineUsersProvider realtimeUsersProvider;
        private readonly HashSet<OnlineUserData> onlineUsersBuffer = new ();

        public CompositeOnlineFriendsProvider(IOnlineUsersProvider apiUsersProvider,
            IOnlineUsersProvider realtimeUsersProvider)
        {
            this.apiUsersProvider = apiUsersProvider;
            this.realtimeUsersProvider = realtimeUsersProvider;
        }

        public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(CancellationToken ct)
        {
            onlineUsersBuffer.Clear();

            IReadOnlyCollection<OnlineUserData> allOnlineUsers = await apiUsersProvider.GetAsync(ct);

            foreach (OnlineUserData onlineUserData in allOnlineUsers)
                onlineUsersBuffer.Add(onlineUserData);

            IReadOnlyCollection<OnlineUserData> realtimeOnlineFriends = await realtimeUsersProvider.GetAsync(ct);

            foreach (OnlineUserData realtimeOnlineFriend in realtimeOnlineFriends)
                onlineUsersBuffer.Add(realtimeOnlineFriend);

            return onlineUsersBuffer;
        }
    }
}
