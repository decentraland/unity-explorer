using Global.Dynamic;
using NUnit.Framework;
using System;

namespace Global.Tests.EditMode
{
    /// <summary>
    ///     Pins the realm host policy that decides whether the untrusted-realm consent prompt is shown. The negative
    ///     cases matter more than the positive ones: each one is a host an attacker can register or reach, and a
    ///     regression there turns a deep link into a silent realm switch.
    /// </summary>
    public class TrustedRealmsShould
    {
        // The E2E fixture pool. The subdomain is minted per run, so the policy has to cover it by domain.
        [TestCase("https://f-9c24e0fe1464661d.e2e-fixtures.decentraland.zone")]
        [TestCase("https://f-0000000000000000.e2e-fixtures.decentraland.zone/")]
        [TestCase("https://f-9c24e0fe1464661d.e2e-fixtures.decentraland.zone:8443")]
        [TestCase("https://F-9C24E0FE1464661D.E2E-FIXTURES.DECENTRALAND.ZONE")]
        // Any depth under the controlled non-production domain, including the apex.
        [TestCase("https://decentraland.zone")]
        [TestCase("https://anything.decentraland.zone")]
        [TestCase("https://a.b.c.decentraland.zone")]
        public void TrustControlledZoneHosts(string realm) =>
            Assert.IsTrue(TrustedRealms.IsTrusted(new Uri(realm)), realm);

        // Domain-level trust is https-only, so a cleartext realm inside the domain still needs consent.
        [TestCase("http://f-9c24e0fe1464661d.e2e-fixtures.decentraland.zone")]
        [TestCase("http://decentraland.zone")]
        public void NotTrustCleartextInsideAControlledDomain(string realm) =>
            Assert.IsFalse(TrustedRealms.IsTrusted(new Uri(realm)), realm);

        // Suffix matching must be anchored to a label boundary at the end of the host.
        [TestCase("https://evildecentraland.zone")]
        [TestCase("https://xdecentraland.zone")]
        [TestCase("https://decentraland.zone.example.com")]
        [TestCase("https://f-1.e2e-fixtures.decentraland.zone.example.com")]
        [TestCase("https://decentraland.zonely.io")]
        public void NotTrustLookAlikeDomains(string realm) =>
            Assert.IsFalse(TrustedRealms.IsTrusted(new Uri(realm)), realm);

        // Production and 'today' are not generalised to whole domains: only the named hosts below are trusted.
        [TestCase("https://anything.decentraland.org")]
        [TestCase("https://e2e-fixtures.decentraland.org")]
        [TestCase("https://decentraland.org")]
        [TestCase("https://anything.decentraland.today")]
        public void NotTrustWholeProductionOrTodayDomains(string realm) =>
            Assert.IsFalse(TrustedRealms.IsTrusted(new Uri(realm)), realm);

        // The exact hosts that were trusted before the domain tier existed, including scheme-agnostic loopback.
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

        // Uri.Host is the only input to the decision. A trusted-looking string anywhere else in the url --
        // userinfo, query, fragment -- must not leak trust to the host that actually gets contacted.
        [TestCase("https://f-9c24e0fe1464661d.e2e-fixtures.decentraland.zone@evil.example.com")]
        [TestCase("https://evil.example.com#f-1.e2e-fixtures.decentraland.zone")]
        [TestCase("https://evil.example.com/?realm=f-1.e2e-fixtures.decentraland.zone")]
        // The fully qualified form fails closed: cheaper to ask for consent than to assume the trailing dot is safe.
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
