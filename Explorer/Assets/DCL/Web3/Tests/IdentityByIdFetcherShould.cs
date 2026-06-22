using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Web3;
using DCL.Web3.Abstract;
using DCL.Web3.Authenticators;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Nethereum.Signer;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Web3.Authenticators.Tests
{
    public class IdentityByIdFetcherShould
    {
        private const string AUTH_API_URL = "https://auth-api.test.decentraland.org";
        private const string IDENTITY_ID = "abc-123";
        private const string SIGNER = "0x1234567890abcdef1234567890abcdef12345678";
        private const string EXPIRATION_ISO = "2030-01-15T10:20:30.000Z";

        // Valid 32-byte secp256k1 private key (Hardhat account #0) so the inner EthECKey ctor accepts it.
        private const string PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

        private IWebRequestController webRequestController;
        private IWeb3AccountFactory web3AccountFactory;
        private IWeb3Account ephemeralAccount;
        private IdentityByIdFetcher fetcher;

        [SetUp]
        public void SetUp()
        {
            webRequestController = Substitute.For<IWebRequestController>();
            web3AccountFactory = Substitute.For<IWeb3AccountFactory>();
            ephemeralAccount = Substitute.For<IWeb3Account>();
            web3AccountFactory.CreateAccount(Arg.Any<EthECKey>()).Returns(ephemeralAccount);

            fetcher = new IdentityByIdFetcher(URLAddress.FromString(AUTH_API_URL), webRequestController, web3AccountFactory);
        }

        [Test]
        public async Task BuildIdentityFromResponse()
        {
            StubGet(ValidResponse());

            DecentralandIdentity identity = await fetcher.FetchAsync(IDENTITY_ID, IWeb3Identity.Web3IdentitySource.Deeplink, CancellationToken.None);

            Assert.That(identity.Address == SIGNER, Is.True);
            Assert.That(identity.Expiration, Is.EqualTo(DateTime.Parse(EXPIRATION_ISO, null, DateTimeStyles.RoundtripKind)));
            Assert.That(identity.EphemeralAccount, Is.SameAs(ephemeralAccount));
            Assert.That(identity.AuthChain.Get(AuthLinkType.SIGNER).payload, Is.EqualTo(SIGNER));
        }

        [TestCase(IWeb3Identity.Web3IdentitySource.Deeplink)]
        [TestCase(IWeb3Identity.Web3IdentitySource.TokenFile)]
        public async Task PassThroughTheRequestedSource(IWeb3Identity.Web3IdentitySource source)
        {
            StubGet(ValidResponse());

            DecentralandIdentity identity = await fetcher.FetchAsync(IDENTITY_ID, source, CancellationToken.None);

            Assert.That(identity.Source, Is.EqualTo(source));
        }

        [Test]
        public void PropagateWebRequestErrors()
        {
            webRequestController.SendAsync<GenericGetRequest, GenericGetArguments,
                                     GenericDownloadHandlerUtils.CreateFromJsonOp<IdentityAuthResponseDto, GenericGetRequest>, IdentityAuthResponseDto>(
                                     Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                                     Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<IdentityAuthResponseDto, GenericGetRequest>>())!
                                .Returns(UniTask.FromException<IdentityAuthResponseDto>(new Exception("network down")));

            Assert.That(async () => await fetcher.FetchAsync(IDENTITY_ID, IWeb3Identity.Web3IdentitySource.Deeplink, CancellationToken.None),
                Throws.Exception.With.Message.EqualTo("network down"));
        }

        private void StubGet(IdentityAuthResponseDto dto) =>
            webRequestController.SendAsync<GenericGetRequest, GenericGetArguments,
                                     GenericDownloadHandlerUtils.CreateFromJsonOp<IdentityAuthResponseDto, GenericGetRequest>, IdentityAuthResponseDto>(
                                     Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                                     Arg.Any<GenericDownloadHandlerUtils.CreateFromJsonOp<IdentityAuthResponseDto, GenericGetRequest>>())!
                                .Returns(UniTask.FromResult<IdentityAuthResponseDto>(dto));

        private static IdentityAuthResponseDto ValidResponse() =>
            new ()
            {
                identity = new IdentityAuthResponseDto.IdentityDto
                {
                    expiration = EXPIRATION_ISO,
                    ephemeralIdentity = new IdentityAuthResponseDto.EphemeralIdentityDto { privateKey = PRIVATE_KEY },
                    authChain = new List<AuthLink>
                    {
                        new () { type = AuthLinkType.SIGNER, payload = SIGNER, signature = "" },
                        new () { type = AuthLinkType.ECDSA_EPHEMERAL, payload = "Decentraland Login", signature = "0xdeadbeef" },
                    },
                },
            };
    }
}
