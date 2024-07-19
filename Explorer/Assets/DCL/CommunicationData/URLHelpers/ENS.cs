using System;
using System.Text.RegularExpressions;

namespace CommunicationData.URLHelpers
{
    public readonly struct ENS
    {
        private readonly string ens;

        public ENS(string ens)
        {
            this.ens = ens;
        }

        public bool IsValid => !string.IsNullOrEmpty(ens) && Regex.IsMatch(ens, @"^[a-zA-Z0-9.]+\.eth$");

        public bool Equals (ENS other) => Equals(other.ens);

        public bool Equals(string other) =>
            string.Equals(ens, other, StringComparison.OrdinalIgnoreCase);

        public override string ToString() => ens;
    }

    public static class ENSUtils
    {
        private const string WORLD_URL = "https://worlds-content-server.decentraland.org/world/";

        public static string ConvertEnsToWorldUrl(ENS ens) =>
            WORLD_URL + ens.ToString().ToLower();
    }

}
