using DCL.Multiplayer.Connections.DecentralandUrls;
using Utility.Types;

namespace Global.Dynamic.ChatCommands
{
    public class EnvironmentValidator
    {
        private readonly DecentralandEnvironment dclEnvironment;

        private readonly string zoneDescription;
        private readonly string orgDescription;


        public EnvironmentValidator(DecentralandEnvironment dclEnvironment)
        {
            this.dclEnvironment = dclEnvironment;
            zoneDescription = DecentralandEnvironment.Zone.ToString().ToLower();
            orgDescription = DecentralandEnvironment.Org.ToString().ToLower();
        }

        public Result ValidateTeleport(string realmToTeleportTo)
        {
            switch (dclEnvironment)
            {
                case DecentralandEnvironment.Today:
                    return Result.ErrorResult(
                        "🔴 Error. You cannot change realms in the Today environment. Please restart DCL with the desired environment");
                case DecentralandEnvironment.Zone:
                    if (realmToTeleportTo.Contains(zoneDescription))
                        return Result.SuccessResult();
                    return Result.ErrorResult(
                        "🔴 Error. You cannot teleport to other realms that are not Zone in Zone environment. Please restart DCL with the desired environment");
                case DecentralandEnvironment.Org:
                    if (realmToTeleportTo.Contains(orgDescription))
                        return Result.SuccessResult();

                    return Result.ErrorResult(
                        "🔴 Error. You cannot teleport to other realms that are not Org or World in Org environment. Please restart DCL with the desired environment");
            }

            return Result.SuccessResult();
        }
    }
}