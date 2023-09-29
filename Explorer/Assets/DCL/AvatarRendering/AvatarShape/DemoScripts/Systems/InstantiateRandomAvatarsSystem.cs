using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.DemoScripts.Components;
using DCL.AvatarRendering.AvatarShape.DemoScripts.UI;
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
using Object = UnityEngine.Object;
using PointerPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.IWearable[], DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableByParamIntention>;
using Random = UnityEngine.Random;
using RaycastHit = UnityEngine.RaycastHit;

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
            var male = new AvatarRandomizerHelper(WearablesLiterals.BodyShape.MALE);
            var female = new AvatarRandomizerHelper(WearablesLiterals.BodyShape.FEMALE);

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
                AvatarRandomizerHelper currentRandomizer = i % 2 == 0 ? male : female;

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
                if(Physics.Raycast(pos, Vector3.down, out hitInfo, distance)) {
                    transform.localPosition = hitInfo.point;
                }
                else
                {
                    transform.localPosition = new Vector3(pos.x, 0.0f, pos.z);
                }
                var transformComp = new TransformComponent(transform);

                var avatarShape = new PBAvatarShape
                {
                    BodyShape = currentRandomizer.BodyShape,
                    Name = i.ToString(),
                    Wearables = { wearables.ToArray() },
                };
                World.Create(avatarShape, transformComp);
            }
        }

        public override void Dispose()
        {
            view.addRandomAvatarButton.onClick.RemoveAllListeners();
            Object.Destroy(view.gameObject);
        }
    }

    public class AvatarRandomizerHelper
    {
        public string BodyShape;
        public List<string> upper_body;
        public List<string> lower_body;
        public List<string> feet;
        public List<string> hair;
        public List<string> mouth;
        public List<string> eyes;
        public List<string> eyebros;

        public AvatarRandomizerHelper(string bodyShape)
        {
            BodyShape = bodyShape;
            upper_body = new List<string>();
            lower_body = new List<string>();
            feet = new List<string>();
            hair = new List<string>();
            mouth = new List<string>();
            eyes = new List<string>();
            eyebros = new List<string>();
        }

        public string[] GetRandomAvatarWearables()
        {
            return new[]
            {
                upper_body[Random.Range(0, upper_body.Count)],
                lower_body[Random.Range(0, lower_body.Count)],
                feet[Random.Range(0, feet.Count)],
                hair[Random.Range(0, hair.Count)],

                //TODO: We still dont have the default asset bundles for this ones
                //We should add them before using them
                //mouth[Random.Range(0, mouth.Count)].metadata.id,
                //eyes[Random.Range(0, eyes.Count)].metadata.id,
                //eyebros[Random.Range(0, eyebros.Count)].metadata.id,
            };
        }

        public void AddWearable(IWearable wearable)
        {
            if (!wearable.IsCompatibleWithBodyShape(BodyShape))
                return;

            switch (wearable.GetCategory())
            {
                case WearablesLiterals.Categories.UPPER_BODY:
                    upper_body.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.LOWER_BODY:
                    lower_body.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.FEET:
                    feet.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.HAIR:
                    hair.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.MOUTH:
                    mouth.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.EYES:
                    eyes.Add(wearable.GetUrn());
                    break;
                case WearablesLiterals.Categories.EYEBROWS:
                    eyebros.Add(wearable.GetUrn());
                    break;
            }
        }
    }
}
