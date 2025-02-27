using UnityEngine;

namespace MVC
{
    public abstract class SimpleView<TViewData> : MonoBehaviour
    {
        protected internal abstract void Setup(TViewData viewData);
    }
}
