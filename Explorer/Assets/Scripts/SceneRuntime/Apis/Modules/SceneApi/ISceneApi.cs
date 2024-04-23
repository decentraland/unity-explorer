using DCL.Diagnostics;
using DCL.Ipfs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.SceneApi
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
            public string contents;

            /// <summary>
            ///     JSON serialization of the entity.metadata field.
            /// </summary>
            public string metadata;

            /// <summary>
            ///     The base URL used to resolve all content files.
            /// </summary>
            public string baseUrl;

            private static readonly List<ContentDefinition> EMPTY_LIST = new ();

            public GetSceneResponse(string cid, List<ContentDefinition>? contents, string metadata, string baseUrl)
            {
                this.cid = cid;

                if (contents == null)
                    ReportHub.LogWarning(
                        ReportCategory.JAVASCRIPT,
                        "Passing empty contents GetSceneResponse from Unity side"
                    );

                this.contents = JsonConvert.SerializeObject(contents ?? EMPTY_LIST);
                this.metadata = metadata;
                this.baseUrl = baseUrl;
            }
        }
    }
}
