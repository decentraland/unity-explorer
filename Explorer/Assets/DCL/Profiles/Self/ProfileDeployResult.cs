using REnum;

namespace DCL.Profiles.Self
{
    public enum ProfileDeployError : byte
    {
        /// <summary>No wallet is signed in, or the identity changed while the edit was being deployed.</summary>
        NoIdentity,

        /// <summary>The edit is identical to the known profile.</summary>
        NothingChanged,

        /// <summary>The catalyst did not take the edit and the local state was reverted.</summary>
        DeployFailed,

        Cancelled,
    }

    [REnum(EnumUnderlyingType.Byte)]
    [REnumField(typeof(Profile), "Ok")]
    [REnumField(typeof(ProfileDeployError), "Error")]
    public readonly partial struct ProfileDeployResult
    {
        /// <summary>True when the wait for the deploy was cancelled instead of answered.</summary>
        public bool IsCancelled => IsError(out ProfileDeployError error) && error == ProfileDeployError.Cancelled;
    }
}
