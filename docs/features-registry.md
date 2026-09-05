# Features Registry

### What is this?

The Features Registry is a singleton designed with one purpose in mind, be the one place where we check if a feature is enabled or not, be it from a Feature Flag, App Argument, or any other condition (like if we are in Editor or in Local Scene Development).

Being a singleton means we no longer need to send booleans as parameters to every plugin and controller and we can just check the FeaturesRegistry value.

### Why have this when we already have a singleton for Feature Flags?

Because most features don't rely solely on the FF value, but many times on other features, or can be enabled by app args or need to be disabled in local scene development. This meant having all these conditions spread out throughout the codebase making it harder to determine if a feature should be or not enabled at any given moment.

### How to use it?

The architecture is very simple:
* First we must declare the feature with its own FeatureID, this is normally analogous to their FeatureFlag name. These are numbered because we use FeatureIDs to dynamically enable/disable certain settings.
* Second, we add the feature to the registry, this is done on the construction of the `FeaturesRegistry` instance. It can be done along all other features inside the `SetFeatureStates` method, or done afterwards with a singular `SetFeatureState` if we need to check the state of another feature (for example, Voice Chat is only enabled if Friends and User Blocking is enabled).

Finally, when we need to actually check if the feature is enabled or not, we can just do
`FeaturesRegistry.Instance.IsEnabled(FeatureId.YOUR_FEATURE)` and we will get the defined value or false if there is none.

### Feature Providers

There is a more advanced use-case for features that are not enabled at start-time but depend on other conditions, like if the specific user is allowed to use a feature. For this we have the concept of IFeatureProvider, which is a small class that contains a `UniTask<bool> IsFeatureEnabledAsync`, and we can add any additional custom logic that it might need inside of this class. For an example you can check `CommunitiesFeatureProvider`, which we are not using right now, but serves as a useful example of this logic.

To register this `FeatureProvider` we must use the `RegisterFeatureProvider` method.

Finally, to check the specific feature so it can calculate if a feature is enabled or not, we use `IsEnabledAsync`, which will call the IsFeatureEnabledAsync of the FeatureProvider and do any calculation it might need to do to figure out if a feature should be enabled or not for this specific user.

In this case, to avoid repeated requests for example, we must make sure that the `FeatureProvider` correctly caches the value and properly resets it if the conditions change (like, if we were depending on a feature enabled for a specific profile, we must erase the cached value if the user logs off).
