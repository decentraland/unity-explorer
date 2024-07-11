using System.Text.RegularExpressions;

namespace DCL.Utilities
{
    public static class EnsUtils
    {
        private const string WORLD_URL = "https://worlds-content-server.decentraland.org/world/";

        public static string ConvertEnsToWorldUrl(string ens) =>
            WORLD_URL + ens.ToLower();

        public static bool ValidateEns(string ens) =>
            !string.IsNullOrEmpty(ens) && Regex.IsMatch(ens, @"^[a-zA-Z0-9.]+\.eth$");
    }
}
