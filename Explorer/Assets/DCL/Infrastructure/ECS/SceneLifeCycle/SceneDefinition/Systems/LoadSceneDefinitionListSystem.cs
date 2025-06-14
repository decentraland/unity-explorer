using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utilities;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Loads a scene list originated from pointers
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class LoadSceneDefinitionListSystem : LoadSystemBase<SceneDefinitions, GetSceneDefinitionList>
    {
        private readonly IWebRequestController webRequestController;

        // cache
        private readonly StringBuilder bodyBuilder = new ();
        private static readonly SceneMetadataConverter SCENE_METADATA_CONVERTER = new ();

        private readonly ProfilerMarker deserializationSampler;

        // There is no cache for the list but a cache per entity that is stored in ECS itself
        internal LoadSceneDefinitionListSystem(World world, IWebRequestController webRequestController,
            IStreamableCache<SceneDefinitions, GetSceneDefinitionList> cache)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;

            deserializationSampler = new ProfilerMarker($"{nameof(LoadSceneDefinitionListSystem)}.Deserialize");
        }

        protected override async UniTask<StreamableLoadingResult<SceneDefinitions>> FlowInternalAsync(GetSceneDefinitionList intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            bodyBuilder.Clear();
            bodyBuilder.Append("{\"pointers\":[");

            for (var i = 0; i < intention.Pointers.Count; ++i)
            {
                int2 pointer = intention.Pointers[i];

                // String Builder has overloads for int to prevent allocations
                bodyBuilder.Append('\"');
                bodyBuilder.Append(pointer.x);
                bodyBuilder.Append(',');
                bodyBuilder.Append(pointer.y);
                bodyBuilder.Append('\"');

                if (i != intention.Pointers.Count - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            return new StreamableLoadingResult<SceneDefinitions>(new SceneDefinitions(await webRequestController.PostAsync(intention.CommonArguments, GenericUploadArguments.CreateJson(bodyBuilder.ToString()), GetReportData())
                                                                                                                .OverwriteFromJsonAsync(intention.TargetCollection, WRJsonParser.Newtonsoft, ct)));

            // TODO This logic should be modified to work with Stream (according to the expectations of BestHTTP and the corresponding generalization)

            /*var adapter = webRequestController.PostAsync(intention.CommonArguments,
                GenericUploadArguments.CreateJson(bodyBuilder.ToString()), GetReportData());

            //using var downloadHandler = await adapter.ExposeDownloadHandlerAsync();
            var nativeData = new NativeArray<byte>(1, Allocator.None); // downloadHandler.nativeData;

            await UniTask.SwitchToThreadPool();

            using (deserializationSampler.Auto())
            {
                var serializer = JsonSerializer.CreateDefault();
                serializer.Converters.Add(SCENE_METADATA_CONVERTER);

                unsafe
                {
                    var dataPtr = (byte*)nativeData.GetUnsafeReadOnlyPtr();

                    serializer.Context = new StreamingContext(0,
                        new SceneMetadataConverterContext(dataPtr));

                    using var stream = new UnmanagedMemoryStream(dataPtr, nativeData.Length,
                        nativeData.Length, FileAccess.Read);

                    using var textReader = new StreamReader(stream, Encoding.UTF8);
                    using var jsonReader = new JsonTextReader(textReader);

                    serializer.Populate(jsonReader, intention.TargetCollection);
                }
            }

            return new StreamableLoadingResult<SceneDefinitions>(
                new SceneDefinitions(intention.TargetCollection));*/
        }

        private sealed class SceneMetadataConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) =>
                typeof(SceneMetadata).IsAssignableFrom(objectType);

            public override object ReadJson(JsonReader reader, Type objectType, object? existingValue,
                JsonSerializer serializer)
            {
                var jsonReader = (JsonTextReader)reader;

                if (jsonReader.LineNumber != 1)
                    throw new NotImplementedException("Can't parse multi-line json");

                var context = (SceneMetadataConverterContext)serializer.Context.Context;
                int readerPosition = jsonReader.LinePosition;

                unsafe
                {
                    byte* dataPtr = context.DataPtr;
                    int charPosition = context.CharPosition;
                    int startByte = context.StartByte;

                    while (charPosition < readerPosition)
                    {
                        int charSize = UTF8Utility.UTF8_CHAR_SIZE[dataPtr[startByte]];
                        startByte += charSize;

                        // Code points that need 4 bytes in UTF-8 need two chars in UTF-16.
                        charPosition += (charSize >> 2) + 1;
                    }

                    // Else, Deserialize will call this converter again and so on until we have a
                    // stack overflow.
                    serializer.Converters.RemoveAt(0);

                    SceneMetadata metadata;
                    try { metadata = serializer.Deserialize<SceneMetadata>(jsonReader); }
                    finally { serializer.Converters.Add(this); }

                    int endByte = startByte;
                    readerPosition = jsonReader.LinePosition;

                    while (charPosition < readerPosition)
                    {
                        int charSize = UTF8Utility.UTF8_CHAR_SIZE[dataPtr[endByte]];
                        endByte += charSize;
                        charPosition += (charSize >> 2) + 1;
                    }

                    // All the complexity here is so that we can obtain this one OriginalJson string
                    // without excess of allocations. Because the sole purpose of this string is to be
                    // passed on to JavaScript, it would be even better if we created a V8Value
                    // directly without decoding the bytes at all.
                    metadata.OriginalJson = Encoding.UTF8.GetString(dataPtr + startByte - 1,
                        endByte - startByte + 1);

                    context.CharPosition = charPosition;
                    context.StartByte = endByte;

                    return metadata;
                }
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
                throw new NotImplementedException();
        }

        private sealed unsafe class SceneMetadataConverterContext
        {
            public readonly byte* DataPtr;
            public int CharPosition;
            public int StartByte;

            public SceneMetadataConverterContext(byte* dataPtr)
            {
                DataPtr = dataPtr;
            }
        }
    }
}
