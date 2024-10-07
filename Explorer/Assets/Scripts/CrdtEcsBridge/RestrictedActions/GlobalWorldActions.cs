using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.ECSComponents;
using DCL.Multiplayer.Emotes;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.GLTF;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using SceneRunner.Scene;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility.Arch;
using SceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromRealmIntention>;
using LocalSceneEmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetSceneEmoteFromLocalDevelopmentSceneIntention>;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.Unity.GLTFContainer.Asset.Components.GltfContainerAsset, ECS.Unity.GLTFContainer.Asset.Components.GetGltfContainerAssetIntention>;
namespace CrdtEcsBridge.RestrictedActions
{
    public class GlobalWorldActions : IGlobalWorldActions
    {
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IEmotesMessageBus messageBus;
        private readonly bool localSceneDevelopment;

        public bool LocalSceneDevelopment => localSceneDevelopment;

        public GlobalWorldActions(World world, Entity playerEntity, IEmotesMessageBus messageBus, bool localSceneDevelopment)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.messageBus = messageBus;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        public void MoveAndRotatePlayer(Vector3 newPlayerPosition, Vector3? newCameraTarget)
        {
            // Move player to new position (through InterpolateCharacterSystem -> TeleportPlayerQuery)
            world.AddOrSet(playerEntity, new PlayerTeleportIntent(newPlayerPosition, Vector2Int.zero));

            // Rotate player to look at camera target (through RotateCharacterSystem -> ForceLookAtQuery)
            if (newCameraTarget != null)
                world.AddOrSet(playerEntity, new PlayerLookAtIntent(newCameraTarget.Value));
        }

        public void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition)
        {
            if (newCameraTarget == null || world == null)
                return;

            // Rotate camera to look at new target (through ApplyCinemachineCameraInputSystem -> ForceLookAtQuery)
            SingleInstanceEntity camera = world.CacheCamera();
            world.AddOrSet(camera, new CameraLookAtIntent(newCameraTarget.Value, newPlayerPosition));
        }

        public async UniTask TriggerSceneEmoteAsync(string sceneId, SceneAssetBundleManifest abManifest, string emoteHash, bool loop, CancellationToken ct)
        {
            if (!world.TryGet(playerEntity, out AvatarShapeComponent avatarShape))
                throw new Exception("Cannot resolve body shape of current player because its missing AvatarShapeComponent");

            var promise = SceneEmotePromise.Create(world,
                new GetSceneEmoteFromRealmIntention(sceneId, abManifest, emoteHash, loop, avatarShape.BodyShape),
                PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            using var consumed = promise.Result!.Value.Asset.ConsumeEmotes();
            var value = consumed.Value[0]!;
            URN urn = value.GetUrn();
            bool isLooping = value.IsLooping();

            TriggerEmote(urn, isLooping);
        }

        public async Task TriggerLocalSceneEmoteAsync(string sceneId, string emotePath, string emoteHash, bool loop, CancellationToken ct)
        {
            if (!world.TryGet(playerEntity, out AvatarShapeComponent avatarShape))
                throw new Exception("Cannot resolve body shape of current player because its missing AvatarShapeComponent");

            var gltfPromise = Promise.Create(world, new GetGltfContainerAssetIntention(emotePath, emoteHash, new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY);
            var component = new GltfContainerComponent(ColliderLayer.ClNone, ColliderLayer.ClNone, gltfPromise);
            component.State = LoadingState.Loading;
            var entity = world.Create(component);

            //var res = await gltfPromise.ToUniTaskAsync(world,cancellationToken:ct);

            //var gltfRoot = res.Result.Value.Asset.Root;

            while (!component.Promise.IsConsumed)
            {
                await Task.CompletedTask;
            }

            var gltfRoot = component.Promise.Result.Value.Asset.Root;
            var promise = LocalSceneEmotePromise.Create(world,
                new GetSceneEmoteFromLocalDevelopmentSceneIntention(sceneId, emoteHash,gltfRoot, avatarShape.BodyShape, loop),
                PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            var asd = 2;

            //var gltfIntention = GetGLTFIntention.Create(emotePath, emoteHash);
            //world.Create(GetGLTFIntention.Create(emotePath, emoteHash));

            // var promise = Promise.Create(world, new GetGltfContainerAssetIntention(emotePath, emoteHash, new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY);
            // var component = new GltfContainerComponent(ColliderLayer.ClNone, ColliderLayer.ClNone, promise);
            // component.State = LoadingState.Loading;
            // var entity = world.Create(component);

            // var test = await promise.ToUniTaskAsync(world, cancellationToken: ct);
            // while (!promise.IsConsumed)
            // {
            //     await Task.CompletedTask;
            // }



            //world.Add(entity, component);







            // if (!world.TryGet(playerEntity, out AvatarShapeComponent avatarShape))
            //     throw new Exception("Cannot resolve body shape of current player because its missing AvatarShapeComponent");


            //world.Add(playerEntity, GetGLTFIntention.Create(emotePath, emoteHash));



            // var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // //
            // var promise = LocalSceneEmotePromise.Create(world,
            //     new GetGltfContainerAssetIntention(emotePath,emoteHash, cts),
            //     //new GetLocalSceneEmoteIntention(emoteHash, loop, avatarShape.BodyShape),
            //     PartitionComponent.TOP_PRIORITY);
            //
            // promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            // using var consumed = promise.Result!.Value.Asset.ConsumeEmotes();
            // var value = consumed.Value[0]!;
            // URN urn = value.GetUrn();
            // bool isLooping = value.IsLooping();
            //
            // TriggerEmote(urn, isLooping);

            //var a = hash;
            // TriggerEmote(new URN(emotePath), loop);
        }

        public void TriggerEmote(URN urn, bool isLooping)
        {
            world.Add(playerEntity, new CharacterEmoteIntent { EmoteId = urn, Spatial = true, TriggerSource = TriggerSource.SCENE });
            messageBus.Send(urn, isLooping);
        }
    }
}
