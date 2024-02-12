using LiveKit.client_sdk_unity.Runtime.Scripts.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;

namespace DCL.Multiplayer.Connections.Pools
{
    public static class MultiPoolExtensions
    {
        public static SmartWrap<T> TempResource<T>(this IMultiPool multiPool) where T: class, new() =>
            new (multiPool.Get<T>(), multiPool);
    }
}
