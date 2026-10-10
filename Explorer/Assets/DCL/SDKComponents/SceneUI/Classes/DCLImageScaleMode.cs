namespace DCL.SDKComponents.SceneUI.Classes
{
    public enum DCLImageScaleMode
    {
        // Traditional slicing
        NineSlices = 0,

        // Does not scale, draws in a pixel-perfect model relative to the object center
        Center = 1,

        // Scales the texture, maintaining aspect ratio, so it completely fits withing the position rectangle passed to GUI.DrawTexture
        // Corresponds to Sprite's ScaleMode.ScaleToFit.
        // Applies custom UVs
        Stretch = 2,
    }
}
