using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.RequestsHub;
using ECS;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using SceneRuntime.Apis.Modules.SignedFetch.Messages;
using SceneRunner.Scene;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace SceneRuntime.Apis.Modules.SignedFetch.Tests
{
    public class SignedFetchWrapShould
    {
        /// <summary>
        ///     Player loop ticks granted to a dispatched request to resume after the main thread is released.
        /// </summary>
        private const int MAX_CONTINUATION_TICKS = 10;

        private IWebRequestController webController;
        private ISceneData sceneData;
        private IRealmData realmData;
        private IWeb3IdentityCache identityCache;
        private CancellationTokenSource disposeCts;
        private SignedFetchWrap signedFetchWrap;

        private Vector2Int sceneBase;
        private string sceneID;

        private string realmName;
        private string realmHostname;
        private string realmProtocol;


        [SetUp]
        public void SetUp()
        {
            webController = Substitute.For<IWebRequestController>();
            sceneData = Substitute.For<ISceneData>();
            realmData = Substitute.For<IRealmData>();
            identityCache = Substitute.For<IWeb3IdentityCache>();
            disposeCts = new CancellationTokenSource();

            sceneBase = new Vector2Int(10, 20);
            sceneID = "test-scene-id";
            realmName = "test-realm";
            realmHostname = "test-realm-host";
            realmProtocol = "test-realm-protocol";
            // Setup scene data
            var sceneMetadata = new SceneMetadata
            {
                scene = new SceneMetadataScene
                {
                    DecodedBase = sceneBase,
                }
            };

            var sceneEntityDefinition = new SceneEntityDefinition(sceneID, sceneMetadata);
            sceneData.SceneEntityDefinition.Returns(sceneEntityDefinition);
            sceneData.SceneShortInfo.Returns(new SceneShortInfo(sceneBase, sceneID));

            // Setup realm data
            realmData.Hostname.Returns(realmHostname);
            realmData.Protocol.Returns(realmProtocol);
            realmData.RealmName.Returns(realmName);

            // Setup identity cache with a mock identity
            var mockIdentity = Substitute.For<IWeb3Identity>();
            // Return a new AuthChain with a signer link each time Sign is called
            mockIdentity.Sign(Arg.Any<string>()).Returns(callInfo =>
            {
                var authChain = AuthChain.Create();
                authChain.SetSigner("0x1234567890123456789012345678901234567890");
                return authChain;
            });
            identityCache.Identity.Returns(mockIdentity);

            signedFetchWrap = new SignedFetchWrap(
                webController,
                DecentralandEnvironment.Org,
                sceneData,
                realmData,
                identityCache,
                disposeCts
            );
        }

        [TearDown]
        public void TearDown()
        {
            disposeCts?.Dispose();
        }

        [Test]
        public void UseCorrectMetadataWhenInvokingGetSignedHeaders()
        {
            // Arrange
            string url = "https://example.com/api";
            string body = "";
            string headers = "{}";
            string method = "GET";

            // Act
            string result = signedFetchWrap.GetSignedHeaders(url, body, headers, method) as string;

            // Assert
            Assert.IsNotNull(result, "GetSignedHeaders should return a non-null result");

            // Parse the returned headers JSON
            Dictionary<string, string>? headersDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
            Assert.IsNotNull(headersDict, "Headers should be deserializable");

            // Extract the signature metadata from headers
            Assert.IsTrue(headersDict.ContainsKey("x-identity-metadata"), "Headers should contain x-identity-metadata");
            string signatureMetadataJson = headersDict["x-identity-metadata"];

            // Parse the signature metadata JSON
            SignedFetchWrap.SignatureMetadata metadata = JsonUtility.FromJson<SignedFetchWrap.SignatureMetadata>(signatureMetadataJson);
            Assert.IsNotNull(metadata, "Signature metadata should be deserializable");

            DoAssertions(metadata);


        }

        private void DoAssertions(SignedFetchWrap.SignatureMetadata metadata)
        {
            // Assert the signer is always "decentraland-kernel-scene"
            Assert.AreEqual(
                "decentraland-kernel-scene",
                metadata.signer,
                "CreateSignatureMetadata must always use 'decentraland-kernel-scene' as the signer. This is critical for security and must not be changed."
            );

            // Assert the parcel has the correct coords
            Assert.AreEqual(
                $"{sceneBase.x},{sceneBase.y}",
                metadata.parcel,
                "CreateSignatureMetadata must always have the scene base in the metadata. This is critical for security and must not be changed."
            );

            // Assert the parcel has the correct Scene Base
            Assert.AreEqual(
                sceneID,
                metadata.sceneId,
                "CreateSignatureMetadata must always have the scene ID in the metadata. This is critical for security and must not be changed."
            );

            // Assert the realm name is the correct one has the correct Scene Base
            Assert.AreEqual(
                realmHostname,
                metadata.realm.hostname,
                "CreateSignatureMetadata must always have the realm hostname in the metadata. This is critical for security and must not be changed."
            );

            Assert.AreEqual(
                realmProtocol,
                metadata.realm.protocol,
                "CreateSignatureMetadata must always have the realm protocol in the metadata. This is critical for security and must not be changed."
            );

            Assert.AreEqual(
                realmName,
                metadata.realm.serverName,
                "CreateSignatureMetadata must always have the realm server Name in the metadata. This is critical for security and must not be changed."
            );
        }

        [Test]
        public void UseCorrectMetadataWhenInvokingCreatingSignatureMetadata()
        {
            // Arrange - Test CreateSignatureMetadata directly (now internal for testing)
            string? hashPayload = null;

            // Act
            string signatureMetadataJson = signedFetchWrap.CreateSignatureMetadata(hashPayload);

            // Assert
            Assert.IsNotNull(signatureMetadataJson, "CreateSignatureMetadata should return a non-null result");

            // Parse the signature metadata JSON
            SignedFetchWrap.SignatureMetadata metadata = JsonUtility.FromJson<SignedFetchWrap.SignatureMetadata>(signatureMetadataJson);
            Assert.IsNotNull(metadata, "Signature metadata should be deserializable");

            DoAssertions(metadata);
        }

        /// <summary>
        ///     Scene code calls signed fetch from the scene thread, so the request only reaches the web
        ///     request controller after a hop to the main thread. The scene runtime owns <c>disposeCts</c> and
        ///     disposes it while unloading, which can land inside that hop: reading <c>disposeCts.Token</c>
        ///     from the continuation threw <see cref="ObjectDisposedException" /> and the request was lost.
        /// </summary>
        [UnityTest]
        public IEnumerator DispatchRequestWithTokenCapturedBeforeMainThreadHop()
        {
            // Arrange
            string url = "https://example.com/api";
            string body = "";
            string headers = "{}";
            string method = "get";

            CancellationToken tokenBeforeDisposal = disposeCts.Token;
            CancellationToken dispatchedToken = default;
            var dispatched = false;

            webController.RequestHub.Returns(Substitute.For<IRequestHub>());

            webController.When(controller =>
                            controller.SendAsync<GenericGetRequest, GenericGetArguments, FlatFetchResponse<GenericGetRequest>, FlatFetchResponse>(
                                Arg.Any<RequestEnvelope<GenericGetRequest, GenericGetArguments>>(),
                                Arg.Any<FlatFetchResponse<GenericGetRequest>>(),
                                Arg.Any<long>(),
                                Arg.Any<IProgress<float>>()))
                         .Do(callInfo =>
                          {
                              dispatchedToken = callInfo.Arg<RequestEnvelope<GenericGetRequest, GenericGetArguments>>().Ct;
                              dispatched = true;
                          });

            Exception? dispatchFailure = null;

            // The main thread stays parked on Join() for the whole dispatch, so the request cannot leave
            // its hop to the main thread before the source is disposed.
            var sceneThread = new Thread(() =>
            {
                try { signedFetchWrap.SignedFetch(url, body, headers, method); }
                catch (ArgumentNullException)
                {
                    // The returned promise is built by ClearScript from the script engine running on this
                    // thread, and no EditMode test has one; the request is dispatched before that happens.
                }
                catch (Exception e) { dispatchFailure = e; }

                disposeCts.Dispose();
            });

            // Act
            sceneThread.Start();
            sceneThread.Join();

            for (var i = 0; i < MAX_CONTINUATION_TICKS && !dispatched; i++)
                yield return null;

            // Assert
            Assert.IsNull(dispatchFailure, $"Dispatching the request threw {dispatchFailure}");
            Assert.IsTrue(dispatched, "The request never reached the web request controller.");
            Assert.AreEqual(tokenBeforeDisposal, dispatchedToken, "The request must carry the token read before the source was disposed.");
        }
    }
}

