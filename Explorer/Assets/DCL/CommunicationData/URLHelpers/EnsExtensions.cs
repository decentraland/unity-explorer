using CommunicationData.URLHelpers;
using System.Text.RegularExpressions;

namespace DCL.CommunicationData.URLHelpers
{
    public static class EnsExtensions
    {
        private static readonly Regex REGEX = new (@"^[a-zA-Z0-9.]+\.eth$");

        public static bool IsEns(this string str) =>
            REGEX.Match(str).Success;

        public static bool IsEns(this URLDomain domain) =>
            REGEX.Match(domain.Value).Success;

        /// <summary>
        /// Builds the world URL for the given ENS using the environment-aware base URL
        /// (e.g. from IDecentralandUrlsSource.Url(DecentralandUrl.WorldServer)).
        /// </summary>
        public static string ConvertEnsToWorldUrl(this ENS ens, string worldContentServerBaseUrl)
        {
            string baseUrl = worldContentServerBaseUrl.TrimEnd('/');
            return $"{baseUrl}/{ens.ToString().ToLower()}";
        }
    }
}
