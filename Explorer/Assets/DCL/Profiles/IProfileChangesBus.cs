using System;

namespace DCL.Profiles
{
    public interface IProfileChangesBus : IDisposable
    {
        void PushProfileNameChange(Profile profile);
        void SubscribeToProfileNameChange(ProfileChangesBusController.ProfileChangedDelegate callback);
        void UnsubscribeToProfileNameChange(ProfileChangesBusController.ProfileChangedDelegate callback);
    }
}
