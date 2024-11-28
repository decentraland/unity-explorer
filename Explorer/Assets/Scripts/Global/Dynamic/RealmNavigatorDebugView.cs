using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;

namespace Global.Dynamic
{
    public class RealmNavigatorDebugView
    {
        private readonly ElementBinding<string> currentRealmName;
        private readonly ElementBinding<string> currentCatalystLambdas;
        private readonly ElementBinding<string> currentCatalystContent;


        public RealmNavigatorDebugView(IDebugContainerBuilder debugContainerBuilder)
        {
            currentRealmName = new ElementBinding<string>("");
            currentCatalystLambdas = new ElementBinding<string>("");
            currentCatalystContent = new ElementBinding<string>("");

            debugContainerBuilder
                .TryAddWidget(IDebugContainerBuilder.Categories.REALM)?
                .AddCustomMarker(new ElementBinding<string>("Current Realm"))
                .AddCustomMarker(currentRealmName)
                .AddCustomMarker(new ElementBinding<string>("Current Lambdas Catalyst"))
                .AddCustomMarker(currentCatalystLambdas)
                .AddCustomMarker(new ElementBinding<string>("Current Content Catalyst"))
                .AddCustomMarker(currentCatalystContent);
        }

        public void UpdateRealmName(string newRealm, string catalystLambdas, string catalystContent)
        {
            currentRealmName.Value = newRealm;
            currentCatalystLambdas.Value = catalystLambdas;
            currentCatalystContent.Value = catalystContent;
        }
    }
}