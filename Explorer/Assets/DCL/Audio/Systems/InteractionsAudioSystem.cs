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
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [UpdateBefore(typeof(WritePointerEventResultsSystem))]
    public partial class InteractionsAudioSystem : BaseUnityLoopSystem
    {

        private readonly IInteractionsAudioConfigs interactionsAudioConfigs;

        private InteractionsAudioSystem(World world, IInteractionsAudioConfigs interactionsAudioConfigs) : base(world)
        {
            this.interactionsAudioConfigs = interactionsAudioConfigs;
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

            foreach (byte validIndex in intent.ValidIndices)
            {
                PBPointerEvents.Types.Entry entry = pbPointerEvents.PointerEvents[validIndex];

                if (entry.EventType == PointerEventType.PetDown)
                {
                    PBPointerEvents.Types.Info info = entry.EventInfo;
                    switch (info.Button)
                    {
                        case InputAction.IaPointer:
                            UIAudioEventsBus.Instance.SendPlayAudioEvent(interactionsAudioConfigs.PointerAudio);
                            break;
                        case InputAction.IaPrimary:
                            UIAudioEventsBus.Instance.SendPlayAudioEvent(interactionsAudioConfigs.PrimaryAudio);
                            break;
                        case InputAction.IaSecondary:
                            UIAudioEventsBus.Instance.SendPlayAudioEvent(interactionsAudioConfigs.SecondaryAudio);
                            break;
                    }
                }
            }
        }

    }
}
