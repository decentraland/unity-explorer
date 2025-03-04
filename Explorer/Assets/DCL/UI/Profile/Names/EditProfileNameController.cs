using Cysharp.Threading.Tasks;
using MVC;
using System;
using System.Threading;

namespace DCL.UI.ProfileNames
{
    public class EditProfileNameController : ControllerBase<EditProfileNameView, EditProfileNameParams>
    {
        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        public EditProfileNameController(ViewFactoryMethod viewFactory) : base(viewFactory)
        {
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
