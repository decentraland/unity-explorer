namespace DCL.PrivateWorlds
{
    /// <summary>
    /// Holds the validated password (shared secret) for the current world.
    /// Set after successful password validation, read by the comms handshake layer
    /// to include the secret in the connection metadata.
    /// Cleared on realm change.
    /// </summary>
    public interface IWorldCommsSecret
    {
        string? Secret { get; set; }
    }

    public class WorldCommsSecret : IWorldCommsSecret
    {
        public string? Secret { get; set; }
    }
}
