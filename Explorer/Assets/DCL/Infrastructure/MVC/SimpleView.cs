using UnityEngine;

namespace MVC
{
    public abstract class SimpleView<TViewData> : MonoBehaviour
    {
        public abstract void Setup(TViewData viewData);
    }
}
