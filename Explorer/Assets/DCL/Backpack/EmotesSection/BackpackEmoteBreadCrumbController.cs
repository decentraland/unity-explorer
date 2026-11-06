using DCL.Backpack.BackpackBus;
using DCL.Diagnostics;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace DCL.Backpack.EmotesSection
{
    public class BackpackEmoteBreadCrumbController
    {
        private const string SLOT_ID_VARIABLE = "slotId";

        private readonly BackpackEmoteBreadCrumbView view;
        private readonly IntVariable localizedSlotId;

        public BackpackEmoteBreadCrumbController(BackpackEmoteBreadCrumbView view,
            IBackpackEventBus eventBus)
        {
            this.view = view;

            if (view.CategoryLabel.StringReference.TryGetValue(SLOT_ID_VARIABLE, out IVariable? variable) && variable is IntVariable slotIdVariable)
            {
                localizedSlotId = slotIdVariable;
            }
            else
            {
                ReportHub.LogError(ReportCategory.BACKPACK, $"Emote breadcrumb label has no int variable named {SLOT_ID_VARIABLE}");
                localizedSlotId = new IntVariable();
            }

            eventBus.EmoteSlotSelectEvent += OnSlotSelected;
        }

        private void OnSlotSelected(int slot)
        {
            view.SlotLabel.text = slot.ToString();
            localizedSlotId.Value = slot;
        }
    }
}
