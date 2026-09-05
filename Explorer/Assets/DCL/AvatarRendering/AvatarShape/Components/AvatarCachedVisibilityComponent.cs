namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarCachedVisibilityComponent
    {
        public bool IsVisible;
        private DITHER_STATE currentDitherState;

        public bool ShouldUpdateDitherState(float newDistance, float startFadeDithering, float endFadeDithering)
        {
            if (newDistance >= startFadeDithering && currentDitherState != DITHER_STATE.Opaque)
            {
                currentDitherState = DITHER_STATE.Opaque;
                return true;
            }

            if (newDistance <= endFadeDithering && currentDitherState != DITHER_STATE.Transparent)
            {
                currentDitherState = DITHER_STATE.Transparent;
                return true;
            }

            if (newDistance > endFadeDithering && newDistance < startFadeDithering)
            {
                currentDitherState = DITHER_STATE.Dithering;
                return true;
            }

            return false;
        }

        public void ResetDitherState()
        {
            currentDitherState = DITHER_STATE.Uninitialized;
        }
    }

    public enum DITHER_STATE
    {
        Uninitialized,
        Transparent,
        Dithering,
        Opaque,
    }
}
