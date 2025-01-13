using System;

namespace DCL.Friends.UI
{
    public class BlockedUsersController : IDisposable
    {
        private readonly BlockedUsersView view;

        public BlockedUsersController(BlockedUsersView view)
        {
            this.view = view;

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
