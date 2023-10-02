using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.DemoScripts.Components;
using DCL.AvatarRendering.AvatarShape.DemoScripts.UI;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Transforms.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using PointerPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableByParamIntention>;
using Random = UnityEngine.Random;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.AvatarRendering { }

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    //TODO: Temporary class to load default wearables and create random avatars from it.
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class InstantiateRandomAvatarsSystem : BaseUnityLoopSystem
    {
        private readonly AvatarInstantiatorView view;
        private SingleInstanceEntity camera;

        public InstantiateRandomAvatarsSystem(World world, AvatarInstantiatorView avatarInstantiatorView) : base(world)
        {
            view = avatarInstantiatorView;
            view.addRandomAvatarButton.onClick.AddListener(AddRandomAvatar);
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        private void AddRandomAvatar()
        {
            World.Create(new AddRandomAvatarIntention
            {
                avatarsToInstantiate = view.GetAvatarsToInstantiate(),
                addSkinnedMeshRendererAvatar = view.GetDoSkin(),
            });
        }

        protected override void Update(float t)
        {
            StartRandomAvatarInstantiationQuery(World);
            FinalizeRandomAvatarInstantiationQuery(World, in camera.GetCameraComponent(World));
        }

        [Query]
        [None(typeof(RandomAvatarRequest))]
        private void StartRandomAvatarInstantiation(in Entity entity, ref AddRandomAvatarIntention addRandomAvatarIntention)
        {
            var randomAvatarRequest = new RandomAvatarRequest();

            randomAvatarRequest.BaseWearablesPromise = ParamPromise.Create(World,
                new GetWearableByParamIntention(new[] { ("collectionType", "base-wearable"), ("pageSize", "50") }, "DummyUser", new List<IWearable>()),
                PartitionComponent.TOP_PRIORITY);

            World.Add(entity, randomAvatarRequest);
        }

        [Query]
        private void FinalizeRandomAvatarInstantiation(in Entity entity, [Data] in CameraComponent cameraComponent, ref RandomAvatarRequest randomAvatarRequest, ref AddRandomAvatarIntention addRandomAvatarIntention)
        {
            if (randomAvatarRequest.BaseWearablesPromise.TryConsume(World, out StreamableLoadingResult<IWearable[]> baseWearables))
            {
                if (baseWearables.Succeeded)
                    GenerateRandomAvatars(baseWearables.Asset, addRandomAvatarIntention.avatarsToInstantiate, addRandomAvatarIntention.addSkinnedMeshRendererAvatar, cameraComponent.Camera.transform.position);
                else
                    ReportHub.LogError(GetReportCategory(), "Base wearables could't be loaded!");

                World.Destroy(entity);
            }
        }

        private void GenerateRandomAvatars(IWearable[] defaultWearables, int randomAvatarsToInstantiate, bool addSkinnedMeshRenderer, Vector3 cameraPosition)
        {
            var male = new AvatarRandomizer(BodyShape.MALE);
            var female = new AvatarRandomizer(BodyShape.FEMALE);

            foreach (IWearable wearable in defaultWearables)
            {
                male.AddWearable(wearable);
                female.AddWearable(wearable);
            }

            float startXPosition = cameraPosition.x;
            float startZPosition = cameraPosition.z;

            //hacky spawn size
            float density = 2.0f;
            float spawnArea = (float)Math.Sqrt(randomAvatarsToInstantiate) * density;

            var currentXCounter = 0;
            var currentYCounter = 0;

            for (var i = 0; i < randomAvatarsToInstantiate; i++)
            {
                AvatarRandomizer currentRandomizer = i % 2 == 0 ? male : female;

                float halfSpawnArea = spawnArea / 2;
                float randomX = Random.Range(-halfSpawnArea, halfSpawnArea);
                float randomZ = Random.Range(-halfSpawnArea, halfSpawnArea);

                var wearables = new List<string>();

                foreach (string randomAvatarWearable in currentRandomizer.GetRandomAvatarWearables())
                    wearables.Add(randomAvatarWearable);

                // Create a transform, normally it will be created either by JS Scene or by Comms
                Transform transform = new GameObject($"RANDOM_AVATAR_{i}").transform;

                Vector3 pos  = new Vector3(startXPosition + randomX, 500, startZPosition + randomZ);
                RaycastHit hitInfo = new RaycastHit();
                float distance = 1000.0f;

                if (Physics.Raycast(pos, Vector3.down, out hitInfo, distance))
                    transform.localPosition = hitInfo.point;
                else
                    transform.localPosition = new Vector3(pos.x, 0.0f, pos.z);


                var transformComp = new TransformComponent(transform);

                var avatarShape = new PBAvatarShape
                {
                    BodyShape = currentRandomizer.BodyShape,
                    Id = "0",
                    Wearables = { wearables.ToArray() },
                    SkinColor = WearablesConstants.DefaultColors.GetRandomSkinColor3(),
                    HairColor = WearablesConstants.DefaultColors.GetRandomHairColor3(),
                };
                World.Create(avatarShape, transformComp);

                if (addSkinnedMeshRenderer)
                {
                    Transform transformSkinnedMesh = new GameObject($"RANDOM_AVATAR_{i}").transform;
                    var transformCompSkinnedMesh = new TransformComponent(transformSkinnedMesh);
                    transformCompSkinnedMesh.Transform.transform.position = transform.position + new Vector3(1, 0, 0);

                    var avatarShapeSkinnedMesh = new PBAvatarShape
                    {
                        BodyShape = currentRandomizer.BodyShape,
                        Id = "1",
                        Wearables = { wearables.ToArray() },
                    };

                    World.Create(avatarShapeSkinnedMesh, transformCompSkinnedMesh);
                }
            }
        }

        public override void Dispose()
        {
            view.addRandomAvatarButton.onClick.RemoveAllListeners();
        }
    }
}
