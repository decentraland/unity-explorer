using DCL.Backpack.BackpackBus;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace DCL.Backpack.EmotesSection
{
    public class BackpackEmoteBreadCrumbController
    {
        private readonly BackpackEmoteBreadCrumbView view;
        private readonly IntVariable localizedSlotId;

        public BackpackEmoteBreadCrumbController(BackpackEmoteBreadCrumbView view,
            IBackpackEventBus eventBus)
        {
            this.view = view;
            localizedSlotId = (IntVariable)view.CategoryLabel.StringReference["slotId"];

            eventBus.EmoteSlotSelectEvent += OnSlotSelected;
        }

        private void OnSlotSelected(int slot)
        {
            view.SlotLabel.text = slot.ToString();
            localizedSlotId.Value = slot;
        }
    }
}
