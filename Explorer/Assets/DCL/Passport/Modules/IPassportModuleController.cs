using DCL.Profiles;
using System;

namespace DCL.Passport.Modules
{
    public interface IPassportModuleController : IDisposable
    {
        void Setup(Profile profile);
        void Clear();
    }
}
