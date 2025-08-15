using System;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserRightSectionController : IDisposable
    {
        private readonly CommunitiesBrowserRightSectionView view;

        public CommunitiesBrowserRightSectionController(CommunitiesBrowserRightSectionView view)
        {
            this.view = view;


        }

        public void Dispose()
        {
        }
    }
}
