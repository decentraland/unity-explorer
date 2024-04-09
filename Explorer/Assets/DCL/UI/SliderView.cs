using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class SliderView : MonoBehaviour
    {
        [field: SerializeField]
        public Slider Slider { get; private set; }

        [field: SerializeField]
        public Button DecreaseButton { get; private set; }

        [field: SerializeField]
        public Button IncreaseButton { get; private set; }
    }
}
