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

            public static Key NewKey<TArgs, TWebRequest>() where TArgs: struct where TWebRequest: ITypedWebRequest<TArgs> =>
                new (typeof(TArgs), typeof(TWebRequest));
        }

        private readonly IReadOnlyDictionary<Key, object> map;

        public RequestHub(ITexturesFuse texturesFuse, bool isTextureCompressionEnabled)
        {
            var mutableMap = new Dictionary<Key, object>();
            map = mutableMap;

            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GetTextureArguments args) => new GetTextureWebRequest(envelope, args, controller, texturesFuse, isTextureCompressionEnabled))
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericGetArguments args) => new GenericGetRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericUploadArguments args) => new GenericPostRequest(envelope, args, controller));
        }

        //     Add<GenericGetArguments, GenericGetRequest>(mutableMap, GenericGetRequest.Initialize);
        //     Add<GenericPostArguments, GenericPostRequest>(mutableMap, GenericPostRequest.Initialize);
        //     Add<GenericPutArguments, GenericPutRequest>(mutableMap, GenericPutRequest.Initialize);
        //     Add<GenericDeleteArguments, GenericDeleteRequest>(mutableMap, GenericDeleteRequest.Initialize);
        //     Add<GenericPatchArguments, GenericPatchRequest>(mutableMap, GenericPatchRequest.Initialize);
        //     Add<GenericHeadArguments, GenericHeadRequest>(mutableMap, GenericHeadRequest.Initialize);
        //     Add<GetAudioClipArguments, GetAudioClipWebRequest>(mutableMap, GetAudioClipWebRequest.Initialize);
        //     Add<GetAssetBundleArguments, GetAssetBundleWebRequest>(mutableMap, GetAssetBundleWebRequest.Initialize);
        //     Add<GenericGetArguments, PartialDownloadRequest>(mutableMap, PartialDownloadRequest.Initialize);
        //     Add(mutableMap, (in CommonArguments arguments, GetTextureArguments specificArguments) => GetTextureWebRequest.Initialize(arguments, specificArguments, texturesFuse, isTextureCompressionEnabled));
        // }

        private static void Add<TArgs, TWebRequest>(IDictionary<Key, object> map, InitializeRequest<TArgs, TWebRequest> requestDelegate)
            where TArgs: struct
            where TWebRequest: ITypedWebRequest<TArgs>
        {
            map.Add(Key.NewKey<TArgs, TWebRequest>(), requestDelegate);
        }

        public InitializeRequest<TArgs, TWebRequest> RequestDelegateFor<TArgs, TWebRequest>()
            where TArgs: struct
            where TWebRequest: ITypedWebRequest<TArgs>
        {
            if (map.TryGetValue(Key.NewKey<TArgs, TWebRequest>(), out object requestDelegate))
                return (InitializeRequest<TArgs, TWebRequest>)requestDelegate!;

            throw new InvalidOperationException("Request type not supported.");
        }
    }
}
