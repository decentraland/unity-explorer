using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests.Dumper;
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

        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IReadOnlyDictionary<Key, object> map;
        private bool ktxEnabled;

        public RequestHub(IDecentralandUrlsSource urlsSource, bool disableABCache = false)
        {
            this.urlsSource = urlsSource;
            var mutableMap = new Dictionary<Key, object>();
            map = mutableMap;

            Add<GenericGetArguments, GenericGetRequest>(mutableMap, GenericGetRequest.Initialize);
            Add<GenericPostArguments, GenericPostRequest>(mutableMap, GenericPostRequest.Initialize);
            Add<GenericPostArguments, GenericPutRequest>(mutableMap, GenericPutRequest.Initialize);
            Add<GenericPostArguments, GenericDeleteRequest>(mutableMap, GenericDeleteRequest.Initialize);
            Add<GenericPostArguments, GenericPatchRequest>(mutableMap, GenericPatchRequest.Initialize);
            Add<GenericHeadArguments, GenericHeadRequest>(mutableMap, GenericHeadRequest.Initialize);
            Add<GetAudioClipArguments, GetAudioClipWebRequest>(mutableMap, GetAudioClipWebRequest.Initialize);
            Add(mutableMap, (string url, ref GetAssetBundleArguments abArgs) => GetAssetBundleWebRequest.Initialize(url, abArgs, disableABCache || WebRequestsDebugControl.DisableCache));
            Add<GenericGetArguments, PartialDownloadRequest>(mutableMap, PartialDownloadRequest.Initialize);
            Add(mutableMap, (string url, ref GetTextureArguments specificArguments) => GetTextureWebRequest.Initialize(url, specificArguments, urlsSource, ktxEnabled));
        }

        private void Add<T, TWebRequest>(IDictionary<Key, object> map, InitializeRequest<T, TWebRequest> requestDelegate)
            where T: struct
            where TWebRequest: struct, ITypedWebRequest
        {
            InitializeRequest<T, TWebRequest> invokeWithTransformedUrl
                = (string url, ref T arguments) => requestDelegate.Invoke(urlsSource.TransformUrl(url), ref arguments);

            map.Add(Key.NewKey<T, TWebRequest>(), invokeWithTransformedUrl);
        }

        public InitializeRequest<T, TWebRequest> RequestDelegateFor<T, TWebRequest>()
            where T: struct
            where TWebRequest: struct, ITypedWebRequest
        {
            if (map.TryGetValue(Key.NewKey<T, TWebRequest>(), out object requestDelegate))
                return (InitializeRequest<T, TWebRequest>)requestDelegate!;

            throw new InvalidOperationException("Request type not supported.");
        }

        public void SetKTXEnabled(bool enabled)
        {
            ktxEnabled = enabled;
        }
    }
}
