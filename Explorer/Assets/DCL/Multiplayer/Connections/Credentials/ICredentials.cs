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
}
