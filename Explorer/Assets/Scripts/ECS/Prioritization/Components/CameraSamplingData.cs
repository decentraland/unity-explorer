namespace ECS.Prioritization.Components
{
    /// <summary>
    ///     <para>
    ///         An OOP singleton-like way to set and retrieve partition data connected to the player's camera
    ///     </para>
    ///     <para>
    ///         It does not make sense to store it in the component as this data is resolved once [per frame] and then shared between all scenes/worlds
    ///     </para>
    ///     <para>
    ///         It's added to the camera entity when the profile is resolved and the player is set to the initial position
    ///         to prevent the scene from being resolved prematurely
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
