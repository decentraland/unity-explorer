using System;
using UnityEngine;

namespace DCL.Gizmos
{
    public class DrawSceneGizmosHub : MonoBehaviour
    {
        public struct ProviderState
        {
            internal readonly SceneGizmosProviderBase gizmosProvider;
            internal bool active;

            public ProviderState(SceneGizmosProviderBase gizmosProvider)
            {
                this.gizmosProvider = gizmosProvider;
                active = true;
            }
        }

        private Func<ProviderState[]> providersInitialization;

        private ProviderState[] cachedProviders;

        internal ProviderState[] GetCachedProviders() =>
            cachedProviders;

        internal ProviderState[] gizmosProviders => cachedProviders ??= providersInitialization();

        /// <summary>
        ///     Lazy setup
        /// </summary>
        internal void Setup(Func<ProviderState[]> onInitialize)
        {
            providersInitialization = onInitialize;
        }

        internal void SetGizmosActive(int index, bool active)
        {
            if (index < 0 || index >= gizmosProviders.Length)
                return;

            ref ProviderState el = ref gizmosProviders[index];
            el.active = active;
        }

        private void OnDrawGizmos()
        {
            foreach (ProviderState provider in gizmosProviders)
            {
                if (provider.active)
                    provider.gizmosProvider.OnDrawGizmos();
            }
        }

        private void OnDrawGizmosSelected()
        {
            foreach (ProviderState provider in gizmosProviders)
            {
                if (provider.active)
                    provider.gizmosProvider.OnDrawGizmosSelected();
            }
        }
    }
}
