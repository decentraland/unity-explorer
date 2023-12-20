using System;

namespace DCL.Backpack
{
    public class BackpackInfoPanelController : IDisposable
    {
        private readonly BackpackInfoPanelView backpackInfoPanelView;

        public BackpackInfoPanelController(BackpackInfoPanelView backpackInfoPanelView)
        {
            this.backpackInfoPanelView = backpackInfoPanelView;
        }

        public void Dispose()
        {

        }
    }
}
