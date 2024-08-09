using System;

namespace DCL.UI.ConnectionStatusPanel.StatusEntry
{
    public interface IStatusEntry
    {
        enum Status
        {
            Poor,
            Good,
            Excellent,
        }

        public void ShowReloadButton(Action onClick);

        public void ShowStatus(Status status);
    }
}
