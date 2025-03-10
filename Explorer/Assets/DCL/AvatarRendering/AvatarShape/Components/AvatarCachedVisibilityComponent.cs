namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarCachedVisibilityComponent
    {
        public bool IsVisible;
        private DITHER_STATE currentDitherState;

        public bool ShouldUpdateDitherState(float newDistance, float startFadeDithering, float endFadeDithering)
        {
            if (newDistance >= startFadeDithering && currentDitherState != DITHER_STATE.OPAQUE)
            {
                currentDitherState = DITHER_STATE.OPAQUE;
                return true;
            }

            if (newDistance <= endFadeDithering && currentDitherState != DITHER_STATE.TRANSPARENT)
            {
                currentDitherState = DITHER_STATE.TRANSPARENT;
                return true;
            }

            if (newDistance > endFadeDithering && newDistance < startFadeDithering)
            {
                currentDitherState = DITHER_STATE.DITHERING;
                return true;
            }

            return false;
        }

        public void ResetDitherState()
        {
            currentDitherState = DITHER_STATE.UNINITIALIZED;
        }
    }

    public enum DITHER_STATE
    {
        UNINITIALIZED,
        TRANSPARENT,
        DITHERING,
        OPAQUE,
    }
}
