
using System;

namespace DCL.Profiles
{
    public class ProfileChangesBus : IDisposable
    {
        public delegate void ProfileChangedDelegate(Profile profile);

        private ProfileChangedDelegate? profileChangedDelegate;

        public void Dispose() =>
            profileChangedDelegate = null;

        public void PushUpdate(Profile profile) =>
            profileChangedDelegate?.Invoke(profile);

        public void SubscribeToUpdate(ProfileChangedDelegate callback) =>
            profileChangedDelegate += callback;

        public void UnsubscribeToUpdate(ProfileChangedDelegate callback) =>
            profileChangedDelegate -= callback;
    }
}
