using DCL.WebRequests.GenericDelete;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests.RequestsHub
{
    public class RequestHub : IRequestHub
    {
        private readonly IReadOnlyDictionary<(Type, Type), object> map;

        public RequestHub(ITexturesFuse texturesFuse)
        {
            var mutableMap = new Dictionary<(Type, Type), object>();
            map = mutableMap;

            Add<GenericGetArguments, GenericGetRequest>(mutableMap, GenericGetRequest.Initialize);
            Add<GenericPostArguments, GenericPostRequest>(mutableMap, GenericPostRequest.Initialize);
            Add<GenericPutArguments, GenericPutRequest>(mutableMap, GenericPutRequest.Initialize);
            Add<GenericDeleteArguments, GenericDeleteRequest>(mutableMap, GenericDeleteRequest.Initialize);
            Add<GenericPatchArguments, GenericPatchRequest>(mutableMap, GenericPatchRequest.Initialize);
            Add<GenericHeadArguments, GenericHeadRequest>(mutableMap, GenericHeadRequest.Initialize);
            Add<GetAudioClipArguments, GetAudioClipWebRequest>(mutableMap, GetAudioClipWebRequest.Initialize);
            Add<GetAssetBundleArguments, GetAssetBundleWebRequest>(mutableMap, GetAssetBundleWebRequest.Initialize);
            Add(mutableMap, (in CommonArguments arguments, GetTextureArguments specificArguments) => GetTextureWebRequest.Initialize(arguments, specificArguments, texturesFuse));
        }

        private static void Add<T, TWebRequest>(IDictionary<(Type, Type), object> map, InitializeRequest<T, TWebRequest> requestDelegate)
            where T: struct
            where TWebRequest: struct, ITypedWebRequest
        {
            map.Add((typeof(T), typeof(TWebRequest)), requestDelegate);
        }

        public InitializeRequest<T, TWebRequest> RequestDelegateFor<T, TWebRequest>()
            where T: struct
            where TWebRequest: struct, ITypedWebRequest
        {
            if (map.TryGetValue((typeof(T), typeof(TWebRequest)), out object requestDelegate))
                return (InitializeRequest<T, TWebRequest>)requestDelegate!;

            throw new InvalidOperationException("Request type not supported.");
        }
    }
}
