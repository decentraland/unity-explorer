using UnityEngine;

namespace DCL.Quality
{
    public class FogQualitySettingRuntime : IQualitySettingRuntime
    {
        public bool IsActive => RenderSettings.fog;

        public void SetActive(bool active)
        {
            RenderSettings.fog = active;
        }
    }
}
