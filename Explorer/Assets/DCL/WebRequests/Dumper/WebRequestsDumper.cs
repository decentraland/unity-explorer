using CodeLess.Attributes;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.GenericDelete;
using JetBrains.Annotations;
using Newtonsoft.Json;
using NUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace DCL.WebRequests.Dumper
{
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class WebRequestsDumper
    {
        internal static readonly JsonSerializerSettings SERIALIZER_SETTINGS = new ()
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            ContractResolver = new WebRequestsDumperResolver(),
            Converters = new List<JsonConverter>
            {
                new EnvelopeJsonConverter(),
                new GenericPostArgumentsJsonConverter(),
            },
        };

        private readonly WebRequestDump dump = new ();

        public bool Enabled { get; private set; }

        public string Filter { get; set; } = string.Empty;

        public bool IsMatch(bool signed, string url) =>
            Enabled && !signed && (string.IsNullOrEmpty(Filter) || Regex.IsMatch(url, Filter));

        public WebRequestsAnalyticsContainer? AnalyticsContainer { get; set; }

        public int Count => dump.entries.Count;

        public string Serialize() =>
            JsonConvert.SerializeObject(dump, SERIALIZER_SETTINGS);

        public static WebRequestDump Deserialize(string path) =>
            JsonConvert.DeserializeObject<WebRequestDump>(File.ReadAllText(path), SERIALIZER_SETTINGS);

        public void Add(WebRequestDump.Envelope envelope) =>
            dump.entries.Add(envelope);

        public void Restart()
        {
            dump.entries.Clear();
            Enabled = true;
        }

        public void Stop() =>
            Enabled = false;

        public void Resume() =>
            Enabled = true;
    }

    [Preserve]
    public class WebRequestDump
    {
        internal readonly List<Envelope> entries = new ();

        public IReadOnlyList<Envelope> Entries => entries;

        [Serializable]
        [Preserve]
        public class Envelope
        {
            public enum StatusKind
            {
                // Request is either not sent due to budgeting or is still processing
                NOT_CONCLUDED = 0,
                FAILURE = 1,
                SUCCESS = 2,
            }

            public readonly object Args;
            public readonly Type ArgsType;
            public readonly CommonArguments CommonArguments;
            public readonly WebRequestHeadersInfo? HeadersInfo;

            public readonly Type RequestType;
            public readonly DateTime StartTime;
            public StatusKind Status;
            public DateTime EndTime;

            [JsonIgnore]
            public float Duration;

            // Sign is not supported

            [JsonConstructor]
            internal Envelope(Type requestType, CommonArguments commonArguments, Type argsType, object args, WebRequestHeadersInfo? headersInfo,
                DateTime startTime)
            {
                CommonArguments = commonArguments;
                ArgsType = argsType;
                Args = args;
                HeadersInfo = headersInfo;
                RequestType = requestType;
                Status = StatusKind.NOT_CONCLUDED;
                StartTime = startTime;
            }

            internal void Conclude(StatusKind status, DateTime time)
            {
                Status = status;
                EndTime = time;
                Duration = (float)time.Subtract(StartTime).TotalSeconds;
            }

            public UniTask RecreateWithNoOp(IWebRequestController webRequestController, AssetBundleLoadingMutex assetBundleLoadingMutex, CancellationToken token)
            {
                Type type = typeof(Typed<,>).MakeGenericType(RequestType, ArgsType);
                object? typed = Activator.CreateInstance(type);
                MethodInfo? method = type.GetMethod("SendAsync", BindingFlags.NonPublic | BindingFlags.Instance);
                return (UniTask)method.Invoke(typed, new object[] { webRequestController, assetBundleLoadingMutex, this, token });
            }

            private class Typed<TWebRequest, TWebRequestArgs>
                where TWebRequestArgs: struct
                where TWebRequest: struct, ITypedWebRequest
            {
                [Preserve]
                [UsedImplicitly]
                internal UniTask SendAsync(IWebRequestController webRequestController, AssetBundleLoadingMutex assetBundleLoadingMutex, Envelope envelope, CancellationToken token)
                {
                    if (typeof(TWebRequest) == typeof(GenericGetRequest) && typeof(TWebRequestArgs) == typeof(GenericGetArguments))
                        return webRequestController.GetAsync(envelope.CommonArguments, token, ReportCategory.GENERIC_WEB_REQUEST, envelope.HeadersInfo)
                                                   .WithNoOpAsync();

                    if (typeof(TWebRequest) == typeof(GenericPostRequest) && envelope.Args is GenericPostArguments postArguments)
                        return webRequestController.PostAsync(envelope.CommonArguments, postArguments, token, ReportCategory.GENERIC_WEB_REQUEST, envelope.HeadersInfo)
                                                   .WithNoOpAsync();

                    if (typeof(TWebRequest) == typeof(GenericPutRequest) && envelope.Args is GenericPostArguments putArguments)
                        return webRequestController.PutAsync(envelope.CommonArguments, putArguments, token, ReportCategory.GENERIC_WEB_REQUEST, envelope.HeadersInfo)
                                                   .WithNoOpAsync();

                    if (typeof(TWebRequest) == typeof(GenericPatchRequest) && envelope.Args is GenericPostArguments patchArguments)
                        return webRequestController.PatchAsync(envelope.CommonArguments, patchArguments, token, ReportCategory.GENERIC_WEB_REQUEST, envelope.HeadersInfo)
                                                   .WithNoOpAsync();

                    if (typeof(TWebRequest) == typeof(GenericDeleteRequest) && envelope.Args is GenericPostArguments deleteArguments)
                        return webRequestController.DeleteAsync(envelope.CommonArguments, deleteArguments, token, ReportCategory.GENERIC_WEB_REQUEST, envelope.HeadersInfo)
                                                   .WithNoOpAsync();

                    if (typeof(TWebRequest) == typeof(GetTextureWebRequest) && envelope.Args is GetTextureArguments textureArguments)
                        return webRequestController.GetTextureAsync(envelope.CommonArguments, textureArguments,
                            GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp, FilterMode.Trilinear), token, ReportCategory.GENERIC_WEB_REQUEST);

                    if (typeof(TWebRequest) == typeof(GetAssetBundleWebRequest) && envelope.Args is GetAssetBundleArguments abArguments)
                        return webRequestController.GetAssetBundleAsync(envelope.CommonArguments, new GetAssetBundleArguments(assetBundleLoadingMutex, abArguments.CacheHash, abArguments.AutoLoadAssetBundle),
                            token, headersInfo: envelope.HeadersInfo);

                    throw new NotSupportedException($"\"{typeof(TWebRequest).FullName} & {envelope.Args.GetType()}\" is not supported");
                }
            }
        }
    }
}
