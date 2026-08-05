// ReSharper disable InconsistentNaming

using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    /// <summary>
    ///     Shared fixture for abgen fallback tests: fetches a deployed scene entity from a catalyst and
    ///     exposes its content mapping as an <see cref="ISceneContent" />.
    /// </summary>
    internal class AbgenTestScene : ISceneContent
    {
        public const string CONTENT_URL = "https://peer.decentraland.org/content/contents/";
        public const string GENESIS_PLAZA = "bafkreihkt2hubw5qjyqjpujawkhaixacrmvo54mulwifw2amlzen2znwhm";

        private readonly Dictionary<string, string> fileToHash = new (StringComparer.OrdinalIgnoreCase);

        public URLDomain ContentBaseUrl => URLDomain.FromString(CONTENT_URL);

        private AbgenTestScene(ContentDto[] content)
        {
            foreach (ContentDto entry in content)
                fileToHash[entry.file] = entry.hash;
        }

        public static async UniTask<AbgenTestScene> FetchAsync(string entityId)
        {
            using UnityWebRequest req = UnityWebRequest.Get(CONTENT_URL + entityId);
            await req.SendWebRequest();
            Assert.AreEqual(UnityWebRequest.Result.Success, req.result, $"fetch of entity {entityId} failed: {req.error}");
            return new AbgenTestScene(JsonUtility.FromJson<EntityDto>(req.downloadHandler.text).content);
        }

        public static async UniTask<byte[]> FetchContentAsync(string hash)
        {
            using UnityWebRequest req = UnityWebRequest.Get(CONTENT_URL + hash);
            await req.SendWebRequest();
            Assert.AreEqual(UnityWebRequest.Result.Success, req.result, $"fetch of {hash} failed: {req.error}");
            return req.downloadHandler.data;
        }

        public bool TryGetContentUrl(string contentPath, out URLAddress result)
        {
            bool found = fileToHash.TryGetValue(contentPath, out string hash);
            result = found ? URLAddress.FromString(CONTENT_URL + hash) : URLAddress.EMPTY;
            return found;
        }

        public bool TryGetHash(string name, out string hash) =>
            fileToHash.TryGetValue(name, out hash);

        // Server schema: decentraland/catalyst content API — /content/contents/{entityId} deployed-entity JSON.
        [Serializable]
        private class EntityDto
        {
            public ContentDto[] content = null!;
        }

        [Serializable]
        internal class ContentDto
        {
            public string file = null!;
            public string hash = null!;
        }
    }
}
