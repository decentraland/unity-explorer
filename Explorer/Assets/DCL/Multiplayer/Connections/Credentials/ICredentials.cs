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
    }
}
