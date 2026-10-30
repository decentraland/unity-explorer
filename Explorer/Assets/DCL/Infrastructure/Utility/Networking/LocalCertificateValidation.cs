using System;
using System.Net;
using UnityEngine.Networking;

namespace Utility.Networking
{
    /// <summary>
    ///     Certificate policy for local fixture endpoints. This is disabled by default and only accepts
    ///     certificates for loopback names after the launcher explicitly opts in with --accept-untrusted-realm.
    /// </summary>
    public static class LocalCertificateValidation
    {
        private static bool allowUntrustedLoopbackCertificates;

        public static void Configure(bool allowUntrustedCertificates) => allowUntrustedLoopbackCertificates = allowUntrustedCertificates;

        public static bool ShouldBypass(Uri uri) =>
            allowUntrustedLoopbackCertificates
            && (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase))
            && IsLoopbackHost(uri.Host);

        public static CertificateHandler? CreateCertificateHandler(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && ShouldBypass(uri)
                ? new AcceptAnyLoopbackCertificateHandler()
                : null;
        }

        private static bool IsLoopbackHost(string host)
        {
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
                return true;

            return IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address);
        }

        private sealed class AcceptAnyLoopbackCertificateHandler : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData) => true;
        }
    }
}
