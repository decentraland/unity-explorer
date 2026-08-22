using Global.Dynamic;
using NUnit.Framework;
using System;

namespace Global.Tests.EditMode
{
    /// <summary>
    ///     Pins the host policy behind the untrusted-realm consent prompt. A regression in the negative
    ///     cases turns a deep link into a silent realm switch.
    /// </summary>
    public class TrustedRealmsShould
    {
        // Fixture hosts are minted per run; only the domain tier covers them.
        [TestCase("https://f-9c24e0fe1464661d.e2e-fixtures.decentraland.zone")]
        [TestCase("https://f-0000000000000000.e2e-fixtures.decentraland.zone/")]
        [TestCase("https://f-9c24e0fe1464661d.e2e-fixtures.decentraland.zone:8443")]
        [TestCase("https://F-9C24E0FE1464661D.E2E-FIXTURES.DECENTRALAND.ZONE")]
        // Any depth, apex included.
        [TestCase("https://decentraland.zone")]
        [TestCase("https://anything.decentraland.zone")]
        [TestCase("https://a.b.c.decentraland.zone")]
        public void TrustControlledZoneHosts(string realm) =>
            Assert.IsTrue(TrustedRealms.IsTrusted(new Uri(realm)), realm);

        // Domain trust is https-only.
        [TestCase("http://f-9c24e0fe1464661d.e2e-fixtures.decentraland.zone")]
        [TestCase("http://decentraland.zone")]
        public void NotTrustCleartextInsideAControlledDomain(string realm) =>
            Assert.IsFalse(TrustedRealms.IsTrusted(new Uri(realm)), realm);

        // The suffix must end the host, on a label boundary.
        [TestCase("https://evildecentraland.zone")]
        [TestCase("https://xdecentraland.zone")]
        [TestCase("https://decentraland.zone.example.com")]
        [TestCase("https://f-1.e2e-fixtures.decentraland.zone.example.com")]
        [TestCase("https://decentraland.zonely.io")]
        public void NotTrustLookAlikeDomains(string realm) =>
            Assert.IsFalse(TrustedRealms.IsTrusted(new Uri(realm)), realm);

        // Never whole domains, only the named hosts below.
        [TestCase("https://anything.decentraland.org")]
        [TestCase("https://e2e-fixtures.decentraland.org")]
        [TestCase("https://decentraland.org")]
        [TestCase("https://anything.decentraland.today")]
        public void NotTrustWholeProductionOrTodayDomains(string realm) =>
            Assert.IsFalse(TrustedRealms.IsTrusted(new Uri(realm)), realm);

        // Pre-existing exact hosts; loopback is scheme-agnostic.
        [TestCase("http://127.0.0.1:8000")]
        [TestCase("http://localhost:8000")]
        [TestCase("https://localhost")]
        [TestCase("https://sdk-team-cdn.decentraland.org")]
        [TestCase("https://sdk-test-scenes.decentraland.zone")]
        [TestCase("https://realm-provider-ea.decentraland.org/main")]
        [TestCase("https://realm-provider-ea.decentraland.zone/main")]
        [TestCase("https://worlds-content-server.decentraland.org")]
        [TestCase("https://worlds-content-server.decentraland.zone")]
        public void KeepTrustingTheNamedHosts(string realm) =>
            Assert.IsTrue(TrustedRealms.IsTrusted(new Uri(realm)), realm);

        // Only Uri.Host decides; userinfo, query and fragment must not leak trust.
        [TestCase("https://f-9c24e0fe1464661d.e2e-fixtures.decentraland.zone@evil.example.com")]
        [TestCase("https://evil.example.com#f-1.e2e-fixtures.decentraland.zone")]
        [TestCase("https://evil.example.com/?realm=f-1.e2e-fixtures.decentraland.zone")]
        // Trailing dot fails closed.
        [TestCase("https://f-1.e2e-fixtures.decentraland.zone.")]
        public void NotBeSpoofedByOtherUriParts(string realm) =>
            Assert.IsFalse(TrustedRealms.IsTrusted(new Uri(realm)), realm);

        [TestCase("https://example.com")]
        [TestCase("https://catalyst.example.org")]
        [TestCase("http://192.168.1.10:8000")]
        public void NotTrustUnrelatedHosts(string realm) =>
            Assert.IsFalse(TrustedRealms.IsTrusted(new Uri(realm)), realm);
    }
}
