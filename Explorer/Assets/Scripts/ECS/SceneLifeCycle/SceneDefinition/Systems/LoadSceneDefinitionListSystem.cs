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
using System.Text;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

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

        // There is no cache for the list but a cache per entity that is stored in ECS itself
        internal LoadSceneDefinitionListSystem(World world, IWebRequestController webRequestController,
            IStreamableCache<SceneDefinitions, GetSceneDefinitionList> cache)
            : base(world, cache)
        {
            this.webRequestController = webRequestController;
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

            var adapter = webRequestController.PostAsync(intention.CommonArguments,
                GenericPostArguments.CreateJson(bodyBuilder.ToString()), ct, GetReportData());

            using var downloadHandler = await adapter.ExposeDownloadHandlerAsync();
            var nativeData = downloadHandler.nativeData;
            var serializer = JsonSerializer.CreateDefault();
            var targetList = intention.TargetCollection;

            unsafe
            {
                var dataPtr = (byte*)nativeData.GetUnsafeReadOnlyPtr();

                using var stream = new UnmanagedMemoryStream(dataPtr, nativeData.Length,
                    nativeData.Length, FileAccess.Read);

                using var textReader = new StreamReader(stream, Encoding.UTF8);
                using var jsonReader = new JsonTextReader(textReader);

                jsonReader.Read();

                if (jsonReader.TokenType != JsonToken.StartArray)
                    throw new JsonReaderException(
                        $"Expected token StartArray, got {jsonReader.TokenType}", jsonReader.Path,
                        jsonReader.LineNumber, jsonReader.LinePosition, null);

                int charPosition = 0;
                int startByte = 0;

                while (jsonReader.Read() && jsonReader.TokenType != JsonToken.EndArray)
                {
                    if (jsonReader.TokenType != JsonToken.StartObject)
                        throw new JsonReaderException(
                            $"Expected token StartObject, got {jsonReader.TokenType}", jsonReader.Path,
                            jsonReader.LineNumber, jsonReader.LinePosition, null);

                    int readerPosition = jsonReader.LinePosition;

                    while (charPosition < readerPosition)
                    {
                        int charSize = UTF8Utility.UTF8_CHAR_SIZE[dataPtr[startByte]];
                        charPosition += (charSize >> 2) + 1;
                        startByte += charSize;
                    }

                    var scene = serializer.Deserialize<SceneEntityDefinition>(jsonReader);

                    if (jsonReader.LineNumber != 1)
                        throw new NotImplementedException("Can't parse json that contains newlines");

                    int endByte = startByte;
                    readerPosition = jsonReader.LinePosition;

                    while (charPosition < readerPosition)
                    {
                        int charSize = UTF8Utility.UTF8_CHAR_SIZE[dataPtr[endByte]];
                        charPosition += (charSize >> 2) + 1;
                        endByte += charSize;
                    }

                    if (scene != null)
                    {
                        // All the complexity here is so that we can obtain this one OriginalJson string
                        // without excess of allocations. Because the sole purpose of this string is to
                        // be passed on to JavaScript, it would be even better if we created a V8Value
                        // directly without decoding the bytes at all.
                        scene.metadata.OriginalJson = Encoding.UTF8.GetString(dataPtr + startByte - 1,
                            endByte - startByte + 1);

                        targetList.Add(scene);
                    }

                    startByte = endByte;
                }
            }

            return new StreamableLoadingResult<SceneDefinitions>(new SceneDefinitions(targetList));
        }
    }
}
