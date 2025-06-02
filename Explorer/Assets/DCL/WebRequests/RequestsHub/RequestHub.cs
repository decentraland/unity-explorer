using Best.HTTP.Caching;
using DCL.Multiplayer.Connections.DecentralandUrls;
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
        private bool ktxEnabled;

        public RequestHub(IDecentralandUrlsSource urlsSource, HTTPCache cache, bool partialDownloadingEnabled, long chunkSize, bool ktxEnabled,
            WebRequestsMode mode,
            PartialRequestsDump? partialRequestsDump = null)
        {
            this.ktxEnabled = ktxEnabled;

            var mutableMap = new Dictionary<Key, object>();
            map = mutableMap;

            if (partialRequestsDump)
                partialRequestsDump.Clear();

            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GetTextureArguments args) => new GetTextureWebRequest(envelope, args, controller, this.ktxEnabled, urlsSource));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericGetArguments args) => new GenericGetRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericHeadArguments args) => new GenericHeadRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericUploadArguments args) => new GenericPostRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericUploadArguments args) => new GenericPutRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericUploadArguments args) => new GenericDeleteRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GenericUploadArguments args) => new GenericPatchRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GetAudioClipArguments args) => new GetAudioClipWebRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in GetAssetBundleArguments args) => new GetAssetBundleWebRequest(envelope, args, controller));
            Add(mutableMap, (IWebRequestController controller, in RequestEnvelope envelope, in PartialDownloadArguments args) => new PartialDownloadRequest(cache, envelope, args, controller, chunkSize, partialDownloadingEnabled, mode, partialRequestsDump));
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

        public void SetKTXEnabled(bool enabled)
        {
            ktxEnabled = enabled;
        }
    }
}
