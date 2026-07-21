using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility.Types;
using System;

namespace DCL.Chat.Commands
{
    public class ChatEnvironmentValidator
    {
        private const string ORG_HOST_SUFFIX = "decentraland.org";
        private const string ZONE_HOST_SUFFIX = "decentraland.zone";

        private readonly DecentralandEnvironment dclEnvironment;

        public ChatEnvironmentValidator(DecentralandEnvironment dclEnvironment)
        {
            this.dclEnvironment = dclEnvironment;
        }

        public Result ValidateTeleport(string realmToTeleportTo)
        {
            switch (dclEnvironment)
            {
                case DecentralandEnvironment.Today:
                    return Result.ErrorResult(
                        "🔴 Error. You cannot change realms in the Today environment. Please restart DCL with the desired environment");
                case DecentralandEnvironment.Zone:
                    return HostHasSuffix(realmToTeleportTo, ZONE_HOST_SUFFIX)
                        ? Result.SuccessResult()
                        : Result.ErrorResult(
                            "🔴 Error. You cannot teleport to other realms that are not Zone in Zone environment. Please restart DCL with the desired environment");
                case DecentralandEnvironment.Org:
                    return HostHasSuffix(realmToTeleportTo, ORG_HOST_SUFFIX)
                        ? Result.SuccessResult()
                        : Result.ErrorResult(
                            "🔴 Error. You cannot teleport to other realms that are not Org or World in Org environment. Please restart DCL with the desired environment");
            }

            return Result.SuccessResult();
        }

        /// <summary>
        /// True if the URL's host equals <paramref name="suffix"/> or ends with "." + suffix at a domain
        /// boundary. Reads the host out of the string via a span (no <see cref="Uri"/> allocation); a URL
        /// carrying userinfo ('@') is rejected so it cannot spoof the host (e.g. https://decentraland.org@evil).
        /// </summary>
        private static bool HostHasSuffix(string url, string suffix)
        {
            int schemeIdx = url.IndexOf("://", StringComparison.Ordinal);

            if (schemeIdx < 0)
                return false;

            int hostStart = schemeIdx + 3;
            int hostEnd = hostStart;

            while (hostEnd < url.Length)
            {
                char c = url[hostEnd];

                if (c == '/' || c == '?' || c == '#' || c == ':')
                    break;

                // userinfo is not expected in a realm URL; reject rather than risk host-confusion
                if (c == '@')
                    return false;

                hostEnd++;
            }

            ReadOnlySpan<char> host = url.AsSpan(hostStart, hostEnd - hostStart);

            if (host.Equals(suffix, StringComparison.OrdinalIgnoreCase))
                return true;

            // subdomain boundary match: host ends with ".{suffix}"
            return host.Length > suffix.Length
                   && host[host.Length - suffix.Length - 1] == '.'
                   && host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
