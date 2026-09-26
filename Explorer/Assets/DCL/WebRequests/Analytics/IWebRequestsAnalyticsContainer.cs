using Cysharp.Threading.Tasks;
using System;

namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     Reserve for the future to deeply analyze ongoing web-requests
    /// </summary>
    public interface IWebRequestsAnalyticsContainer
    {
        public static readonly IWebRequestsAnalyticsContainer TEST = new WebRequestsAnalyticsContainer();

        protected internal void OnBeforeBudgeting<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request) where T: struct, ITypedWebRequest where TWebRequestArgs: struct;

        protected internal void OnRequestStarted<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request) where T: struct, ITypedWebRequest where TWebRequestArgs: struct;

        protected internal void OnRequestFinished<T>(T request) where T: ITypedWebRequest;

        protected internal void OnProcessDataFinished<T>(T request) where T: ITypedWebRequest;

        protected internal void OnException<T>(T request, Exception exception) where T: ITypedWebRequest;

        protected internal void OnException<T>(T request, UnityWebRequestException exception) where T: ITypedWebRequest;

        public void Update(float dt);

        public readonly struct RequestType
        {
            public readonly Type Type;
            public readonly string MarkerName;

            public RequestType(Type type, string markerName)
            {
                Type = type;
                MarkerName = markerName;
            }
        }
    }
}
