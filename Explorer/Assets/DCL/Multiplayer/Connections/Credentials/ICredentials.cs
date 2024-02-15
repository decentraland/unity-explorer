using DCL.Multiplayer.Connections.Pools;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Rooms;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Credentials
{
    public interface ICredentials
    {
        string Url { get; }
        string AuthToken { get; }

        class Const : ICredentials
        {
            public Const(string url, string authToken)
            {
                Url = url;
                AuthToken = authToken;
            }

            public string Url { get; }
            public string AuthToken { get; }
        }
    }

    public static class CredentialsExtensions
    {
        public static string ReadableString(this ICredentials credentials) =>
            $"Url: {credentials.Url}, AuthToken: {credentials.AuthToken}";

        public static Task<bool> Connect<T>(this IRoom room, T credentials, CancellationToken token) where T: ICredentials =>
            room.Connect(credentials.Url, credentials.AuthToken, token);

        public static async Task EnsuredConnect<T>(this IRoom room, T credentials, IMultiPool multiPool, CancellationToken token) where T: ICredentials
        {
            bool result = await room.Connect(credentials, token);

            if (result == false)
            {
                multiPool.TryRelease(room);

                throw new InvalidOperationException(
                    $"Cannot connect to room with url: {credentials.Url} with token: {credentials.AuthToken}"
                );
            }
        }

        public static Task EnsuredConnect(this IRoom room, string connectionString, IMultiPool multiPool, CancellationToken token) =>
            room.EnsuredConnect(new ConnectionStringCredentials(connectionString), multiPool, token);
    }
}
