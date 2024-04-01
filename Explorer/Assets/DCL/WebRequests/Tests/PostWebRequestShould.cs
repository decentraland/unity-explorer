using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace DCL.WebRequests.Tests
{
    [TestFixture]
    public class PostWebRequestShould
    {
        [SetUp]
        public void SetUp()
        {
            webRequestController = new WebRequestController(
                Substitute.For<IWebRequestsAnalyticsContainer>(),
                Substitute.For<IWeb3IdentityCache>());
        }

        private WebRequestController? webRequestController;

        private static readonly URLAddress GET = URLAddress.FromString("https://api.apis.guru/v2/list.json");

        private static readonly URLAddress POST = URLAddress.FromString("https://api.restful-api.dev/objects");

        private static readonly string POST_DATA = @"
                                                     {
                                                        ""name"": ""Apple MacBook Pro 16"",
                                                        ""data"": {
                                                           ""year"": 2019,
                                                           ""price"": 1849.99,
                                                           ""CPU model"": ""Intel Core i9"",
                                                           ""Hard disk size"": ""1 TB""
                                                        }
                                                     }
                                                     ";

        [Test]
        public async Task Succeed()
        {
            await webRequestController!.PostAsync(
                                           POST,
                                           GenericPostArguments.CreateJson(POST_DATA),
                                           CancellationToken.None)
                                      .Timeout(TimeSpan.FromMinutes(1));

            // No exception
        }

        [Test]
        public async Task SucceedWithHeaders()
        {
            var headers = new WebRequestHeadersInfo();
            headers.Add("test_header_1", "test_value_1");
            headers.Add("test_header_2", "test_value_2");

            await webRequestController!.PostAsync(
                                            POST,
                                            GenericPostArguments.CreateJson(POST_DATA),
                                            CancellationToken.None,
                                            headersInfo: headers)
                                       .Timeout(TimeSpan.FromMinutes(1));

            // No exception
        }

        [Test]
        public async Task FailWithMultiform()
        {
            LogAssert.ignoreFailingMessages = true;

            UnityWebRequestException? expectedException = null;

            try
            {
                await webRequestController!.PostAsync(
                                               GET,
                                               GenericPostArguments.CreateMultipartForm(new List<IMultipartFormSection>()),
                                               CancellationToken.None)
                                          .Timeout(TimeSpan.FromMinutes(1));
            }
            catch (UnityWebRequestException e) { expectedException = e; }

            Assert.That(expectedException, Is.Not.Null);
            Assert.That(expectedException!.ResponseCode, Is.EqualTo(405)); // Not allowed
        }

        [Test]
        public async Task FailWithWWWForm()
        {
            LogAssert.ignoreFailingMessages = true;

            UnityWebRequestException? expectedException = null;

            try
            {
                await webRequestController!.PostAsync(
                                                GET,
                                                GenericPostArguments.CreateWWWForm(new WWWForm {headers =
                                                {
                                                    ["test_header"] = "test_value",
                                                }}),
                                                CancellationToken.None)
                                           .Timeout(TimeSpan.FromMinutes(1));
            }
            catch (UnityWebRequestException e) { expectedException = e; }

            Assert.That(expectedException, Is.Not.Null);
            Assert.That(expectedException!.ResponseCode, Is.EqualTo(405)); // Not allowed
        }

        [Test]
        public async Task FailWithJson()
        {
            LogAssert.ignoreFailingMessages = true;

            UnityWebRequestException? expectedException = null;

            try
            {
                await webRequestController!.PostAsync(
                                               GET,
                                               GenericPostArguments.CreateJson(POST_DATA),
                                               CancellationToken.None)
                                          .Timeout(TimeSpan.FromMinutes(1));
            }
            catch (UnityWebRequestException e) { expectedException = e; }

            Assert.That(expectedException, Is.Not.Null);
            Assert.That(expectedException!.ResponseCode, Is.EqualTo(405)); // Not allowed
        }
    }
}
