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
            public string Url { get; }
            public string AuthToken { get; }

            public Const(string url, string authToken)
            {
                Url = url;
                AuthToken = authToken;
            }
        }
    }

    public static class CredentialsExtensions
    {
        public static string ReadableString(this ICredentials credentials) =>
            $"Url: {credentials.Url}, AuthToken: {credentials.AuthToken}";

        public static Task<bool> ConnectAsync<T>(this IRoom room, T credentials, CancellationToken token) where T: ICredentials =>
            room.ConnectAsync(credentials.Url, credentials.AuthToken, token, true);

        public static async Task EnsuredConnectAsync<T>(this IRoom room, T credentials, IMultiPool multiPool, CancellationToken token) where T: ICredentials
        {
            bool result = await room.ConnectAsync(credentials, token);

            if (result == false)
            {
                multiPool.TryRelease(room);

                throw new InvalidOperationException(
                    $"Cannot connect to room with url: {credentials.Url} with token: {credentials.AuthToken}"
                );
            }
        }

        public static Task EnsuredConnectAsync(this IRoom room, string connectionString, IMultiPool multiPool, CancellationToken token) =>
            room.EnsuredConnectAsync(new ConnectionStringCredentials(connectionString), multiPool, token);
    }
}
