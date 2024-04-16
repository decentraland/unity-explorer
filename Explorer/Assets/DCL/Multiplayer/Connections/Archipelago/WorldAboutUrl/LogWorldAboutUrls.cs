using DCL.Diagnostics;
using System;

namespace DCL.Multiplayer.Connections.Archipelago.WorldAboutUrl
{
    public class LogWorldAboutUrls : IWorldAboutUrls
    {
        private readonly IWorldAboutUrls origin;
        private readonly Action<string> log;

        public LogWorldAboutUrls(IWorldAboutUrls origin) : this(
            origin,
            ReportHub.WithReport(ReportCategory.ARCHIPELAGO_REQUEST).Log
        ) { }

        public LogWorldAboutUrls(IWorldAboutUrls origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public string AboutUrl(string realmName)
        {
            string result = origin.AboutUrl(realmName);
            log($"AboutUrl for name ({realmName}) => {result}");
            return result;
        }
    }
}
