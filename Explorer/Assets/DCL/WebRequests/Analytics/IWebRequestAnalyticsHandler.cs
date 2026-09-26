using Cysharp.Threading.Tasks;
using System;

namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     Represents a handler responsible for a certain scope
    /// </summary>
    public interface IWebRequestAnalyticsHandler
    {
        public void Update(float dt);

        public void OnBeforeBudgeting<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request) where T: struct, ITypedWebRequest where TWebRequestArgs: struct;

        public void OnRequestStarted<T, TWebRequestArgs>(in RequestEnvelope<T, TWebRequestArgs> envelope, T request, DateTime startedAt) where T: struct, ITypedWebRequest where TWebRequestArgs: struct;

        public void OnRequestFinished<T>(T request, TimeSpan duration) where T: ITypedWebRequest;

        public void OnProcessDataFinished<T>(T request) where T: ITypedWebRequest;

        public void OnException<T>(T request, Exception exception, TimeSpan duration) where T: ITypedWebRequest;

        public void OnException<T>(T request, UnityWebRequestException exception, TimeSpan duration) where T: ITypedWebRequest;
    }
}
