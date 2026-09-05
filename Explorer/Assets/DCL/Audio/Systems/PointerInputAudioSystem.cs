using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Systems;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.Audio.Systems
{
    [LogCategory(ReportCategory.AUDIO)]
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateBefore(typeof(WritePointerEventResultsSystem))]
    public partial class PointerInputAudioSystem : BaseUnityLoopSystem
    {
        private readonly IPointerInputAudioConfigs pointerInputAudioConfigs;

        private PointerInputAudioSystem(World world, IPointerInputAudioConfigs pointerInputAudioConfigs) : base(world)
        {
            this.pointerInputAudioConfigs = pointerInputAudioConfigs;
        }

        protected override void Update(float t)
        {
            PlayAudioForPointerEventsQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void PlayAudioForPointerEvents(ref PBPointerEvents pbPointerEvents)
        {
            AppendPointerEventResultsIntent intent = pbPointerEvents.AppendPointerEventResultsIntent;
            int count = intent.ValidIndicesCount();

            for (var i = 0; i < count; i++)
            {
                byte validIndex = intent.ValidIndexAt(i);
                PBPointerEvents.Types.Entry entry = pbPointerEvents.PointerEvents[validIndex];

                if (entry.EventType == PointerEventType.PetDown)
                {
                    PBPointerEvents.Types.Info info = entry.EventInfo;

                    switch (info.Button)
                    {
                        case InputAction.IaPointer:
                            UIAudioEventsBus.Instance.SendPlayAudioEvent(pointerInputAudioConfigs.PointerAudio);
                            break;
                        case InputAction.IaPrimary:
                            UIAudioEventsBus.Instance.SendPlayAudioEvent(pointerInputAudioConfigs.PrimaryAudio);
                            break;
                        case InputAction.IaSecondary:
                            UIAudioEventsBus.Instance.SendPlayAudioEvent(pointerInputAudioConfigs.SecondaryAudio);
                            break;
                    }
                }
            }
        }
    }
}
