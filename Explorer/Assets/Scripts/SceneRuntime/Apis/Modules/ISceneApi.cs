using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SceneRuntime.Apis.Modules
{
    public interface ISceneApi : IDisposable
    {
        public GetSceneResponse GetSceneInfo();

        public struct GetSceneResponse
        {
            /// <summary>
            ///     The URN of the scene running, which can be either the entityId or the full URN.
            /// </summary>
            public string cid;

            /// <summary>
            ///     A list containing the contents of the deployed entities.
            /// </summary>
            public List<ContentDefinition> contents;

            /// <summary>
            ///     JSON serialization of the entity.metadata field.
            /// </summary>
            public string metadata;

            /// <summary>
            ///     The base URL used to resolve all content files.
            /// </summary>
            public string baseUrl;
        }
    }
}
