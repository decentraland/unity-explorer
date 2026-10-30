using NUnit.Framework;
using SceneRuntime.Apis.Modules.FetchApi;

namespace CrdtEcsBridge.JsModulesImplementation.Tests
{
    public class SimpleFetchApiSchemeGuardShould
    {
        private const string HTTPS_URL = "https://peer.decentraland.org/lambdas/profiles/0xabc";
        private const string FILE_URL = "file:///C:/local/file.txt";
        private const string LOOPBACK_HTTP_URL = "http://127.0.0.1:8080/internal";

        private static ISimpleFetchApi.Response SuccessfulResponse(string url) =>
            new ()
            {
                Ok = true,
                Status = 200,
                StatusText = "200",
                URL = url,
                Data = "response-body",
                Type = "basic",
            };

        [Test]
        public void KeepHttpsFinalUrlUntouched()
        {
            ISimpleFetchApi.Response result = SimpleFetchApiImplementation.EnforceHttpsFinalScheme(SuccessfulResponse(HTTPS_URL), isLocalSceneDevelopment: false);

            Assert.That(SimpleFetchApiImplementation.ShouldBlockNonHttps(SuccessfulResponse(HTTPS_URL), isLocalSceneDevelopment: false), Is.False);
            Assert.That(result.Ok, Is.True);
            Assert.That(result.URL, Is.EqualTo(HTTPS_URL));
            Assert.That(result.Data, Is.EqualTo("response-body"));
            Assert.That(result.Type, Is.EqualTo("basic"));
        }

        [Test]
        public void BlockFileFinalUrlOutsideLocalSceneDevelopment()
        {
            ISimpleFetchApi.Response result = SimpleFetchApiImplementation.EnforceHttpsFinalScheme(SuccessfulResponse(FILE_URL), isLocalSceneDevelopment: false);

            Assert.That(SimpleFetchApiImplementation.ShouldBlockNonHttps(SuccessfulResponse(FILE_URL), isLocalSceneDevelopment: false), Is.True);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Status, Is.EqualTo(0));
            Assert.That(result.StatusText, Is.EqualTo("Blocked non-https redirect"));
            Assert.That(result.Data, Is.Empty);
            Assert.That(result.Headers, Is.Null);
            Assert.That(result.Type, Is.EqualTo("error"));
            // the final URL is preserved for diagnostics; the body is dropped
            Assert.That(result.URL, Is.EqualTo(FILE_URL));
        }

        [Test]
        public void BlockInternalHttpFinalUrlOutsideLocalSceneDevelopment()
        {
            ISimpleFetchApi.Response result = SimpleFetchApiImplementation.EnforceHttpsFinalScheme(SuccessfulResponse(LOOPBACK_HTTP_URL), isLocalSceneDevelopment: false);

            Assert.That(SimpleFetchApiImplementation.ShouldBlockNonHttps(SuccessfulResponse(LOOPBACK_HTTP_URL), isLocalSceneDevelopment: false), Is.True);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Data, Is.Empty);
            Assert.That(result.Type, Is.EqualTo("error"));
        }

        [Test]
        public void KeepFileFinalUrlInLocalSceneDevelopment()
        {
            ISimpleFetchApi.Response result = SimpleFetchApiImplementation.EnforceHttpsFinalScheme(SuccessfulResponse(FILE_URL), isLocalSceneDevelopment: true);

            Assert.That(SimpleFetchApiImplementation.ShouldBlockNonHttps(SuccessfulResponse(FILE_URL), isLocalSceneDevelopment: true), Is.False);
            Assert.That(result.Ok, Is.True);
            Assert.That(result.URL, Is.EqualTo(FILE_URL));
            Assert.That(result.Data, Is.EqualTo("response-body"));
        }
    }
}
