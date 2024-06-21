using DCL.Profiles;
using System;

namespace DCL.Passport
{
    public interface IPassportModuleController : IDisposable
    {
        void Setup(Profile profile);
    }
}
