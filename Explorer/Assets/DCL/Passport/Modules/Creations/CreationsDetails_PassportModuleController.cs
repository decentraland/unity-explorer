using DCL.Profiles;
using System;

namespace DCL.Passport.Modules.Creations
{
    public class CreationsDetails_PassportModuleController : IPassportModuleController
    {
        private readonly CreationsDetails_PassportModuleView view;

        public CreationsDetails_PassportModuleController(CreationsDetails_PassportModuleView view)
        {
            this.view = view;
        }

        public void Dispose()
        {
        }

        public void Setup(Profile profile)
        {
        }

        public void Clear()
        {
        }
    }
}
