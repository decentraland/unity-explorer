using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility.Types;
using System;

namespace DCL.Chat.Commands
{
    public class ChatEnvironmentValidator
    {
        private readonly DecentralandEnvironment dclEnvironment;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        public ChatEnvironmentValidator(DecentralandEnvironment dclEnvironment, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.dclEnvironment = dclEnvironment;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public Result ValidateTeleport(string realmToTeleportTo)
        {
            // Today serves part of its hosts from .today and the rest from .org, a split pinned when the url source
            // is built, so no realm it could move to would be served consistently.
            if (dclEnvironment == DecentralandEnvironment.Today)
                return Result.ErrorResult(
                    "🔴 Error. You cannot change realms in the Today environment. Please restart DCL with the desired environment");

            // Every other environment — the decentraland ones and a --base-domain deployment alike — accepts exactly
            // the realms under its own base domain, so one check covers them all.
            return HostHasSuffix(realmToTeleportTo, decentralandUrlsSource.BaseDomain)
                ? Result.SuccessResult()
                : Result.ErrorResult(
                    $"🔴 Error. You cannot teleport to realms outside {decentralandUrlsSource.BaseDomain}. Please restart DCL with the desired environment");
        }

        /// <summary>
        /// True if the URL's host equals <paramref name="suffix"/> or ends with "." + suffix at a domain
        /// boundary. Reads the host out of the string via a span (no <see cref="Uri"/> allocation); a URL
        /// carrying userinfo ('@') is rejected so it cannot spoof the host — the '@' is checked before any ':'
        /// so a colon-before-'@' form (https://decentraland.org:1234@evil.com) cannot slip past the guard.
        /// </summary>
        private static bool HostHasSuffix(string url, string suffix)
        {
            int schemeIdx = url.IndexOf("://", StringComparison.Ordinal);

            if (schemeIdx < 0)
                return false;

            int hostStart = schemeIdx + 3;
            int authorityEnd = hostStart;

            while (authorityEnd < url.Length)
            {
                char c = url[authorityEnd];

                if (c == '/' || c == '?' || c == '#')
                    break;

                // userinfo is not expected in a realm URL; reject rather than risk host-confusion. Checked BEFORE
                // any ':' so https://decentraland.org:1234@evil.com (port before userinfo) can't slip past.
                if (c == '@')
                    return false;

                authorityEnd++;
            }

            // Strip the port (if any) from the authority to get the bare host.
            ReadOnlySpan<char> authority = url.AsSpan(hostStart, authorityEnd - hostStart);
            int portSep = authority.IndexOf(':');
            ReadOnlySpan<char> host = portSep >= 0 ? authority.Slice(0, portSep) : authority;

            if (host.Equals(suffix, StringComparison.OrdinalIgnoreCase))
                return true;

            // subdomain boundary match: host ends with ".{suffix}"
            return host.Length > suffix.Length
                   && host[host.Length - suffix.Length - 1] == '.'
                   && host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
