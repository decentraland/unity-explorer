using System;

namespace DCL.Friends.UI
{
    public class FriendsSectionController : IDisposable
    {
        private readonly FriendsSectionView view;

        public FriendsSectionController(FriendsSectionView view)
        {
            this.view = view;

            // this.view.Enable += Enable;
            // this.view.Disable += Disable;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private void Enable()
        {

        }

        private void Disable()
        {

        }
    }
}
