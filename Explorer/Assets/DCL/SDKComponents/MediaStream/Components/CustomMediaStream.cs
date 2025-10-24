namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Identifying media stream parameters produced elsewhere in the Unity code (not related to SDK)
    /// </summary>
    public struct CustomMediaStream
    {
        public float Volume;
        public bool Loop;

        public CustomMediaStream(float volume, bool loop)
        {
            Volume = volume;
            Loop = loop;
        }
    }
}
