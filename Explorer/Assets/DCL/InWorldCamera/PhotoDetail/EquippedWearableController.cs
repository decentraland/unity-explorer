using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class EquippedWearableController
    {
        internal readonly EquippedWearableView view;

		public EquippedWearableController(EquippedWearableView view)
	    {
            this.view = view;
        }

        public UniTask LoadWearable(string urn, CancellationToken ct)
        {

        }

    }
}
