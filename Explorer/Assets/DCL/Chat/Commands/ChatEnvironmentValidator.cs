using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility.Types;
using System;

namespace DCL.Chat.Commands
{
    public class ChatEnvironmentValidator
    {
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        public ChatEnvironmentValidator(IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public Result ValidateTeleport(string realmToTeleportTo)
        {
            // A --gateway session serves every supported service from one origin, realms included, and that origin
            // is allowed to sit outside the base domain — a local e2e fixture's is loopback. It is this session's
            // own infrastructure, named on the command line (DeepLinkAllowlist denies --gateway), so accept it
            // before the domain check rather than rejecting the realms the client is itself routing.
            if (IsOnGatewayOrigin(realmToTeleportTo))
                return Result.SuccessResult();

            // Every environment — the decentraland ones and a --base-domain deployment alike — accepts exactly
            // the realms under its own base domain, so one check covers them all.
            return HostHasSuffix(realmToTeleportTo, decentralandUrlsSource.BaseDomain)
                ? Result.SuccessResult()
                : Result.ErrorResult(
                    $"🔴 Error. You cannot teleport to realms outside {decentralandUrlsSource.BaseDomain}. Please restart DCL with the desired environment");
        }

        /// <summary>
        ///     True when <paramref name="url" /> is served by this session's gateway origin. The origin carries its
        ///     trailing '/', so the comparison stops at the authority boundary: "http://127.0.0.1:8080.attacker.com/"
        ///     does not start with "http://127.0.0.1:8080/". Anything after that boundary is path, which cannot move
        ///     the request off the origin, so no userinfo check is needed here.
        /// </summary>
        private bool IsOnGatewayOrigin(string url) =>
            decentralandUrlsSource.GatewayOrigin is { } gatewayOrigin
            && url.StartsWith(gatewayOrigin, StringComparison.OrdinalIgnoreCase);

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
