using System;
using UnityEngine;

namespace Plugins.NativeWindowManager
{
    public class ResolutionListener: MonoBehaviour
    {
        private Vector2Int storedResolution;

        public event Action<Vector2Int> ResolutionChanged;

        private void Start()
        {
            storedResolution = new Vector2Int(Screen.width, Screen.height);
        }

        private void Update()
        {
            var currentResolution = new Vector2Int(Screen.width, Screen.height);

            if (storedResolution != currentResolution)
            {
                storedResolution = currentResolution;
                ResolutionChanged?.Invoke(currentResolution);
            }
        }
    }
}
