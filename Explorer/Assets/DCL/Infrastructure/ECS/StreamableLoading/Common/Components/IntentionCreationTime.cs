namespace ECS.StreamableLoading.Common.Components
{
    /// <summary>
    ///     Used for debug & profiling purposes
    /// </summary>
    public readonly struct IntentionCreationTime
    {
        public readonly float Value;

        public IntentionCreationTime(float value)
        {
            Value = value;
        }
    }
}
