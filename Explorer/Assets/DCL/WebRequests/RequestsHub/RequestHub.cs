using Best.HTTP.Caching;
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

        public RequestHub(ITexturesFuse texturesFuse, HTTPCache cache, bool isTextureCompressionEnabled, WebRequestsMode webRequestsMode)
        {
            var mutableMap = new Dictionary<Key, object>();
            map = mutableMap;

            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GetTextureArguments args) => new GetTextureWebRequest(envelope, args, controller, texturesFuse, isTextureCompressionEnabled));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericGetArguments args) => new GenericGetRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericHeadArguments args) => new GenericHeadRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericUploadArguments args) => new GenericPostRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericUploadArguments args) => new GenericPutRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericUploadArguments args) => new GenericDeleteRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericUploadArguments args) => new GenericPatchRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GetAudioClipArguments args) => new GetAudioClipWebRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GetAssetBundleArguments args) => new GetAssetBundleWebRequest(envelope, args, controller, webRequestsMode == WebRequestsMode.HTTP2));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in PartialDownloadArguments args) => new PartialDownloadRequest(cache, envelope, args, controller));
        }

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
