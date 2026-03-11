# Diagnostics

## Logs Management

We are using a custom [abstract way for handling reports](https://github.com/decentraland/unity-explorer/tree/main/Explorer/Assets/Scripts/Diagnostics/ReportsHandling).

`ReportHubLogger` replaces the default Unity implementation of [`ILogHandler`](https://docs.unity3d.com/ScriptReference/ILogHandler.html). It allows us to process all logs in a fully controllable custom way and then decide what to do with them, rather than **react** on messages reported from the Unity's engine.

The current capabilities are:
* Providing additional data with each report
   * It is represented by `ReportData` class
   * `Category`: a special marker to quickly recognize a domain the log is reported from. All categories should be defined in `ReportCategory` class
   * `SceneShortInfo`: a coordinate and a name of a scene the log originated from
   * We may include as many additional parameters as needed in the future
* Having as many `IReportHandler`s as needed which allow to:
   * Decide on an individual basis how to process/filter/debounce messages
   * Control what to do with additional data. E.g.:
       * `DebugLogReportHandler` - adds a colored prefix of the `[Category]`.
   * Control the storage/target of logs. Currently, we have 2 targets:
       * `DebugLogReportHandler` - default Unity mechanism
       * Sentry
* A separate `CategorySeverityMatrix` to enable/disable logs per handler. It is provided by the `ReportsHandlingSettings` scriptable object.

Instead of using `Debug.LogXXX` call `ReportHub`: it provides a similar API with extra capabilities. Everything that is reported via a default `Debug.Log` will have default additional data (e.g. `[UNSPECIFIED]` category)

### Integration with systems

`BaseUnityLoopSystem` provides default capabilities to differentiate between categories:
* Category must be specified by an attribute `LogCategoryAttribute`. It can be applied either to the system itself or the group. `Category` applied to the system is prioritized. This capability is provided by the code generation so no reflection is used.
* If no category is specified then a generic `ECS` will be used
* When an exception occurs it will be enriched with the given category
* To log messages manually call `GetReportCategory` from the descendants: it is cached so it does not produce any overhead

It's strongly recommended to introduce a new report category when a new feature domain is developed. It will improve problems investigation in the future.


## Exceptions tolerance

When an exception occurs in the ECS system it is caught and handled by `ISystemGroupExceptionHandler` if such is specified.

The current implementation (`SceneExceptionsHandler`) provides a tolerance for exceptions: no more than `ECS_EXCEPTIONS_PER_MINUTE_TOLERANCE` exception can be tolerated per minute. If this limit is exceeded then a new aggregate `SceneExecutionException` is thrown and the scene execution suspends. The exact behavior of what to do with the faulty scene is instructed by `ISystemGroupExceptionHandler.Action` and driven in the `Unity.SystemGroups` plugin:
* `Suspend` will stop execution but will not call `Dispose` of the systems
* `Dispose` will do both

## Debug Utilities

TODO

## Enable reports to Sentry on development branches

If you need errors/crash reporting on development branches, Sentry is disabled by default. In order to enable it, you need to run manually the [unity-build workflow](https://github.com/decentraland/unity-explorer/actions/workflows/build-unitycloud.yml) with sentry **enabled** selecting your target branch.
You will be able to see reports on: https://decentraland.sentry.io/issues/?environment=development&project=4510719707250688&query=&referrer=issue-list&statsPeriod=24h

<img width="345" height="652" alt="Screenshot 2026-01-21 at 10 39 23" src="https://github.com/user-attachments/assets/16d70fde-4d70-4a55-8079-9e2d062db347" />
