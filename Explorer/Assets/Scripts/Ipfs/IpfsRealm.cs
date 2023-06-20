using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Ipfs
{
    public interface IIpfsRealm
    {
        public string CatalystBaseUrl { get; }
        public string ContentBaseUrl { get; }
        public List<string> SceneUrns { get; }

        public UnityWebRequestAsyncOperation RequestActiveEntitiesByPointers(List<Vector2Int> pointers);
    }

    public class IpfsRealm : IIpfsRealm
    {
        // cache
        private readonly StringBuilder bodyBuilder = new ();

        public IpfsRealm(string realmName, IpfsTypes.ServerAbout serverAbout = null)
        {
            // TODO: realmName resolution, for now just accepts custom realm paths...
            CatalystBaseUrl = realmName;

            if (serverAbout != null)
            {
                SceneUrns = serverAbout.configurations.scenesUrn;
                ContentBaseUrl = serverAbout.content.publicUrl;
                entitiesActiveEndpoint = ContentBaseUrl + "entities/active";
            }
            else
            {
                ContentBaseUrl = CatalystBaseUrl + "content/contents/";
                entitiesActiveEndpoint = CatalystBaseUrl + "content/entities/active";
            }
        }

        public string CatalystBaseUrl { get; }
        public string ContentBaseUrl { get; }

        private readonly string entitiesActiveEndpoint;

        public List<string> SceneUrns { get; }

        public UnityWebRequestAsyncOperation RequestActiveEntitiesByPointers(List<Vector2Int> pointers)
        {
            bodyBuilder.Clear();
            bodyBuilder.Append("{\"pointers\":[");

            for (var i = 0; i < pointers.Count; ++i)
            {
                Vector2Int pointer = pointers[i];
                bodyBuilder.Append($"\"{pointer.x},{pointer.y}\"");

                if (i != pointers.Count - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            var request = UnityWebRequest.Post(entitiesActiveEndpoint, bodyBuilder.ToString(), "application/json");
            return request.SendWebRequest();
        }
    }
}
