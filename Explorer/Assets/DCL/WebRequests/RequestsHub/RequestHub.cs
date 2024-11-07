using DCL.WebRequests.GenericDelete;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Collections.Generic;

namespace DCL.WebRequests.RequestsHub
{
    public class RequestHub : IRequestHub
    {
        private readonly struct Key : IEquatable<Key>
        {
            private readonly Type requestType;
            private readonly Type webType;

            public Key(Type requestType, Type webType)
            {
                this.requestType = requestType;
                this.webType = webType;
            }

            public bool Equals(Key other) =>
                requestType == other.requestType
                && webType == other.webType;

            public override bool Equals(object? obj) =>
                obj is Key other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(requestType, webType);

            public static Key NewKey<T, TWebRequest>() where T: struct where TWebRequest: struct, ITypedWebRequest =>
                new (typeof(T), typeof(TWebRequest));
        }

        private readonly IReadOnlyDictionary<Key, object> map;

        public RequestHub(ITexturesFuse texturesFuse)
        {
            var mutableMap = new Dictionary<Key, object>();
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

        private static void Add<T, TWebRequest>(IDictionary<Key, object> map, InitializeRequest<T, TWebRequest> requestDelegate)
            where T: struct
            where TWebRequest: struct, ITypedWebRequest
        {
            map.Add(Key.NewKey<T, TWebRequest>(), requestDelegate);
        }

        public InitializeRequest<T, TWebRequest> RequestDelegateFor<T, TWebRequest>()
            where T: struct
            where TWebRequest: struct, ITypedWebRequest
        {
            if (map.TryGetValue(Key.NewKey<T, TWebRequest>(), out object requestDelegate))
                return (InitializeRequest<T, TWebRequest>)requestDelegate!;

            throw new InvalidOperationException("Request type not supported.");
        }
    }
}
