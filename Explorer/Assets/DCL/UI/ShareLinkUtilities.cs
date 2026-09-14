using MVC;
using System;

namespace DCL.UI
{
    public static class ShareLinkUtilities
    {
        public static string WithReferrer(string jumpInLink)
        {
            if (ViewDependencies.CurrentIdentity is not { } identity)
                return jumpInLink;

            return $"{jumpInLink}&referrer={identity.Address.ToString()}";
        }

        public static string AsQueryParameterValue(string link) =>
            Uri.EscapeDataString(link);
    }
}
