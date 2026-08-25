using Cysharp.Threading.Tasks;
using ECS;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    /// <summary>
    ///     Passes the current realm through unchanged. Reads live instead of caching because realm
    ///     changes — teleporting between Genesis and a world — must be visible to the very next
    ///     message the bus sends or filters.
    /// </summary>
    public class RealmDataPulseRealm : IPulseRealm
    {
        private readonly IRealmData realmData;

        public string Value => realmData.RealmName;

        public RealmDataPulseRealm(IRealmData realmData)
        {
            this.realmData = realmData;
        }

        public UniTask EnsureResolvedAsync(CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
