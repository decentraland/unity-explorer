using MVC;
using System;

namespace DCL.Friends.UI.Sections.Blocked
{
    public class BlockedSectionController : IDisposable
    {
        private readonly BlockedSectionView view;
        private readonly IMVCManager mvcManager;

        public BlockedSectionController(BlockedSectionView view,
            IMVCManager mvcManager)
        {
            this.view = view;
            this.mvcManager = mvcManager;

            this.view.Enable += Enable;
            this.view.Disable += Disable;
        }

        public void Dispose()
        {
            view.Enable -= Enable;
            view.Disable -= Disable;
        }

        private void Enable()
        {

        }

        private void Disable()
        {

        }
    }
}
