# Third-Party Libraries

> For the Plugins System architecture (how plugins and containers are structured), see [Architecture Overview](architecture-overview.md).

## ArchECS

For maximum performance we utilise [ArchECS](https://github.com/genaray/Arch) which is a high-performance C# based Archetype & Chunks [Entity Component System](https://www.wikiwand.com/en/Entity_component_system) (ECS) for game development and data-oriented programming.

## ClearScript

Decentraland relies on JavaScript as part of the SDK to [create scenes](https://docs.decentraland.org/creator/development-guide/coding-scenes/). So we can embed these scenes and execute them we use [ClearScript](https://github.com/microsoft/ClearScript).

We have a [forked version](https://github.com/decentraland/ClearScript) which includes some memory allocation improvements.

### How to upgrade ClearScript
- Merge changes from the vanilla repo to our own. Likely you will have conflicts so this process should be paid attention to
- Build from our repo [following the official instruction](https://microsoft.github.io/ClearScript/Details/Build.html). Currently, this process is not automated and should be done locally
- Replace DLLs in `Plugins/ClearScript` folder
  - Native libraries for each platform can be taken from the official NuGet as we don't make any changes to them
  - Managed libraries are produced by building from the fork

## Sentry

We use Sentry for performance monitoring and error tracking.

Here is the project details: https://decentraland.sentry.io/projects/unity-explorer/?project=4506075736047616

Here is the list of issues: https://decentraland.sentry.io/issues/?project=4506075736047616

### Local setup

In order to enable the tracking from local builds or the play mode in editor it is needed to add a local file in the project:

```
./Explorer/.sentryconfig.json
```

Which should look like this:

```json
{
  "environment": "development",
  "dsn": "REPLACE_DSN_HERE",
  "release": "0.0.1-local",
  "cli": {
    "auth": ""
  }
}
```

You must override the value `REPLACE_DSN_HERE` for a valid dsn which can be retrieved here: https://decentraland.sentry.io/settings/projects/unity-explorer/keys/

If you don't have access, ask the owner or create your own Sentry project.

### Enable or disable Sentry initialization

1. Go to: `Assets/Scripts/Diagnostics/ReportsHandling/ReportsHandlingSettings.asset`
2. Change the enabled status of `Is Sentry Enabled` toggle.

### Sentry options file

Located at: `Assets/Resources/Sentry/SentryOptions.asset`.

Should be **disabled** by default to prevent unwanted logs. It is later enabled at runtime level in the bootstrap process, see `SentryReportHandler`.

`SentryBuildTimeConfiguration` initializes the values at build-time from (top to bottom in priorities):
- Program arguments: `-sentryEnvironment`, `-sentryDsn`, `-sentryRelease`, `-sentryCliAuthToken`
- `.sentryconfig.json` file
- Environment variables: `SENTRY_ENVIRONMENT`, `SENTRY_DSN`, `SENTRY_RELEASE`, `SENTRY_CLI_AUTH_TOKEN`.
- `Release` value is set from `Application.version`, accordingly set at CI level

#### Environments

The environment value is set either at CI level through program args in build-time

- **development**: designed for `dev` branch, local builds or editor.
- **production**: designed for builds created from `main` branch.
- **branch**: designed for builds created from a custom branch, ie: `feat/my-feat`

#### Debug symbols

They are automatically uploaded in the CI build process. CLI auth token needs to be set (through secrets in CI or through `.sentryconfig.json` in local builds).

Can be previewed here: https://decentraland.sentry.io/settings/projects/unity-explorer/debug-symbols/
