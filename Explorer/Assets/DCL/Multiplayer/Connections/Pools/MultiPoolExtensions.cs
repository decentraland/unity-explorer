using DCL.Multiplayer.Connections.Rooms;
using LiveKit.client_sdk_unity.Runtime.Scripts.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.Pools
{
    public static class MultiPoolExtensions
    {
        public static SmartWrap<T> TempResource<T>(this IMultiPool multiPool) where T: class, new() =>
            new (multiPool.Get<T>(), multiPool);

        public static void TryRelease(this IMultiPool multiPool, IRoom? room)
        {
            switch (room)
            {
                case Room r:
                    multiPool.Release(r);
                    break;
                case LogRoom l:
                    multiPool.Release(l);
                    break;
            }
        }
    }
}
