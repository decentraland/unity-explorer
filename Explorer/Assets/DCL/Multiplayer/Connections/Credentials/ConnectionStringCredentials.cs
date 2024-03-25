using DCL.Utilities.Extensions;
using System;
using System.Collections.Specialized;
using System.Web;

namespace DCL.Multiplayer.Connections.Credentials
{
    public readonly struct ConnectionStringCredentials : ICredentials
    {
        private readonly string connectionString;

        public ConnectionStringCredentials(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public string Url => connectionString
                            .Split('?')
                            .EnsureNotNull("Nothing to split via: {}")[0]!
                            .Replace("livekit:", string.Empty);

        public string AuthToken
        {
            get
            {
                var uri = new Uri(connectionString);
                NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);
                return query["access_token"].EnsureNotNull("Access token not found");
            }
        }
    }
}
