using DCL.DebugUtilities;
using NUnit.Framework;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests.Tests
{
    // Regression coverage for UNITY-EXPLORER-NH7: BindItem threw NRE when
    // ListView rebound a row whose UnityWebRequest had already been disposed
    // by a sibling OnRequestFinished in the same frame.
    public class DebugWebRequestInfoShould
    {
        [Test]
        public void ThrowNullReferenceException_WhenUnityWebRequestMethodIsReadAfterDispose()
        {
            var uwr = new UnityWebRequest("https://example.com", "GET");
            uwr.Dispose();

            Assert.Throws<NullReferenceException>(() => { _ = uwr.method; });
        }

        [Test]
        public void ThrowNullReferenceException_WhenUnityWebRequestUrlIsReadAfterDispose()
        {
            var uwr = new UnityWebRequest("https://example.com", "GET");
            uwr.Dispose();

            Assert.Throws<NullReferenceException>(() => { _ = uwr.url; });
        }

        [Test]
        public void PreserveCachedMethodAndUrl_AfterRequestIsDisposed()
        {
            const string URL = "https://example.com/resource";
            var uwr = new UnityWebRequest(URL, "POST");

            var info = new DebugOngoingWebRequestDef.DebugWebRequestInfo
            {
                Request = uwr,
                StartTime = DateTime.UtcNow,
                ShortenedUrl = "<u>/resource</u>",
                FullUrl = URL,
                Method = uwr.method,
                Duration = 0,
            };

            uwr.Dispose();

            Assert.That(info.Method, Is.EqualTo("POST"));
            Assert.That(info.FullUrl, Is.EqualTo(URL));
        }

        [Test]
        public void AllowRemovalByReferenceEquality_AfterRequestIsDisposed()
        {
            var uwr = new UnityWebRequest("https://example.com", "GET");
            var dataSource = new DebugOngoingWebRequestDef.DataSource();

            dataSource.Add(new DebugOngoingWebRequestDef.DebugWebRequestInfo
            {
                Request = uwr,
                StartTime = DateTime.UtcNow,
                ShortenedUrl = "<u>/</u>",
                FullUrl = "https://example.com",
                Method = "GET",
                Duration = 0,
            });

            Assert.That(dataSource.Requests.Count, Is.EqualTo(1));

            uwr.Dispose();
            dataSource.Remove(uwr);

            Assert.That(dataSource.Requests.Count, Is.EqualTo(0));
        }
    }
}
