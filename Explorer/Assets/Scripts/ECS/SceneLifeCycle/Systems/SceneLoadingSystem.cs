using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using Newtonsoft.Json;
using SceneRunner;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SceneLifeCycleSystem))]
    public partial class SceneLoadingSystem : BaseUnityLoopSystem
    {
        private readonly ISceneFactory sceneFactory;

        public SceneLoadingSystem(World world, ISceneFactory sceneFactory) : base(world)
        {
            this.sceneFactory = sceneFactory;
        }

        protected override void Update(float t)
        {
            ProcessSceneToLoadQuery(World);
        }

        [Query]
        [All(typeof(SceneLoadingComponent))]
        private void ProcessSceneToLoad(in Entity entity, ref SceneLoadingComponent sceneLoadingComponent)
        {
            // If the scene just spawned, we start the request
            if (sceneLoadingComponent.State == SceneLoadingState.Spawned)
            {
                Ipfs.SceneMetadata sceneMetadata = JsonConvert.DeserializeObject<Ipfs.SceneMetadata>(sceneLoadingComponent.Definition.metadata.ToString());

                if (sceneMetadata == null)
                {
                    Debug.LogError("Failed to parse SceneMetadata");
                    sceneLoadingComponent.State = SceneLoadingState.Failed;
                    return;
                }

                //sceneLoadingComponent.Request = Ipfs.RequestContentFile("https://sdk-test-scenes.decentraland.zone/content", sceneLoadingComponent.Definition.content, sceneMetadata.main);
                var sceneCodeContent = sceneLoadingComponent.Definition.content.First(definition => definition.file == sceneMetadata.main);

                sceneLoadingComponent.CancellationToken = new CancellationToken();
                sceneLoadingComponent.Request = sceneFactory.CreateScene("https://sdk-test-scenes.decentraland.zone/content/contents/" + sceneCodeContent.hash, sceneLoadingComponent.CancellationToken);

                sceneLoadingComponent.State = SceneLoadingState.Loading;

                Debug.Log("Spawned Scene: " + JsonConvert.SerializeObject(sceneLoadingComponent));
            }
            else if (sceneLoadingComponent.State == SceneLoadingState.Loading)
            {
                if (sceneLoadingComponent.Request.Status.IsCompleted())
                {
                    Debug.Log("Done!");
                    sceneLoadingComponent.State = SceneLoadingState.Loaded;
                    World.Remove<SceneLoadingComponent>(entity);
                    World.Add<LiveSceneComponent>(entity);
                }
            }
        }
    }
}
