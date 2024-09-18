#if UNITY_EDITOR

using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using UnityEngine;
using UnityEngine.Playables;

namespace DCL.AvatarAnimation
{
    /// <summary>
    /// A special system used for controlling one Timeline (a PlayableDirector component) from the editor while running the app, for demos.
    /// It was necessary to create this system due to the execution order of systems and the Unity timeline was neither predictable nor appropriate
    /// to make functionalities related to playing emotes and moving avatars work.
    /// This will not ship when building the app.
    /// You can find the classes related to the timeline in the DCL.AvatarAnimation.Editor project.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PlayableDirectorUpdatingSystem : BaseUnityLoopSystem
    {
        private PlayableDirector playableDirector;

        public PlayableDirectorUpdatingSystem(World world) : base(world)
        {
            // Empty
        }

        protected override void Update(float t)
        {
            // Just taking the one and only playable director from the scene is ok for now
            if(playableDirector == null)
                playableDirector = GameObject.FindObjectOfType<PlayableDirector>();

            if (playableDirector != null)
            {
                if (playableDirector.state == PlayState.Playing)
                {
                    playableDirector.time += t;
                    playableDirector.Evaluate();

                    if (playableDirector.time > playableDirector.duration)
                    {
                        playableDirector.time = 0.0f;
                        playableDirector.Stop();
                    }
                }
            }
        }
    }
}

#endif
