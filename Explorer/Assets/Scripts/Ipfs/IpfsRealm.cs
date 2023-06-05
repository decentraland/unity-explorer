using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Ipfs
{
    public interface IIpfsRealm
    {
        public UnityWebRequestAsyncOperation RequestActiveEntitiesByPointers(List<Vector2Int> pointers);

        public string CatalystBaseUrl { get; }
        public string ContentBaseUrl { get; }
    }
    public class IpfsRealm : IIpfsRealm
    {
        public string CatalystBaseUrl { get; }
        public string ContentBaseUrl { get; }

        public IpfsRealm(string realmName)
        {
            // TODO: realmName resolution, for now just accepts custom realm paths...
            CatalystBaseUrl = realmName;
            ContentBaseUrl = CatalystBaseUrl + "content/";
        }

        public UnityWebRequestAsyncOperation RequestActiveEntitiesByPointers(List<Vector2Int> pointers)
        {
            StringBuilder bodyBuilder = new StringBuilder("{\"pointers\":[");

            for (int i = 0; i < pointers.Count; ++i)
            {
                var pointer = pointers[i];
                bodyBuilder.Append($"\"{pointer.x},{pointer.y}\"");

                if (i != pointers.Count - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            var request = UnityWebRequest.Post(ContentBaseUrl + "entities/active", bodyBuilder.ToString(), "application/json");
            return request.SendWebRequest();
        }
    }
}
