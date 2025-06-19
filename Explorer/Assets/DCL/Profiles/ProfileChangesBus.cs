
using System;

namespace DCL.Profiles
{
    public class ProfileChangesBus : IDisposable
    {
        public delegate void ProfileChangedDelegate(Profile profile);

        private ProfileChangedDelegate? profileChangedDelegate;

        public void Dispose() =>
            profileChangedDelegate = null;

        public void PushProfileNameChange(Profile profile) =>
            profileChangedDelegate?.Invoke(profile);

        public void SubscribeToProfileNameChange(ProfileChangedDelegate callback) =>
            profileChangedDelegate += callback;

        public void UnsubscribeToProfileNameChange(ProfileChangedDelegate callback) =>
            profileChangedDelegate -= callback;
    }
}
