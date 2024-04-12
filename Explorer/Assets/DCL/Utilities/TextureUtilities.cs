using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public static class TextureUtilities
{
    public static GraphicsFormat GetColorSpaceFormat()
    {
        if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
            return GraphicsFormat.A2R10G10B10_UNormPack32;

        return GraphicsFormat.R32G32B32A32_SFloat;
    }
}
