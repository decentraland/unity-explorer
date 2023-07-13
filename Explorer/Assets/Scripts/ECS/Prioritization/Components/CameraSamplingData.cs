namespace ECS.Prioritization.Components
{
    /// <summary>
    ///     An OOP singleton-like way to set and retrieve partition data connected to the player's camera
    ///     <para>
    ///         It does not make sense to store it in the component as this data is resolved once [per frame] and then shared between all scenes/worlds
    ///     </para>
    /// </summary>
    public class CameraSamplingData : PartitionDiscreteDataBase, IReadOnlyCameraSamplingData
    {
        public CameraSamplingData()
        {
            Clear();
        }
    }
}
