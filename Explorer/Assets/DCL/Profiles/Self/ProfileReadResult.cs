using REnum;

namespace DCL.Profiles.Self
{
    public enum ProfileReadError : byte
    {
        /// <summary>No wallet is signed in.</summary>
        NoIdentity,

        /// <summary>The catalyst holds no profile for the identity.</summary>
        NotFound,

        /// <summary>The catalyst read failed; the cause is the <c>Failed</c> case of <see cref="ProfileKnowledge" />.</summary>
        FetchFailed,

        Cancelled,
    }

    [REnum(EnumUnderlyingType.Byte)]
    [REnumField(typeof(Profile), "Ok")]
    [REnumField(typeof(ProfileReadError), "Error")]
    public readonly partial struct ProfileReadResult { }
}
