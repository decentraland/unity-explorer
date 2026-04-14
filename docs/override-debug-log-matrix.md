# Override the Debug Log Matrix on Build

## What is the Debug Log Matrix?

This matrix controls which logs get logged both in editor and in builds.

<img width="549" height="544" alt="image" src="https://github.com/user-attachments/assets/059b10dc-d97b-4ccd-acc9-f10b49df11c3" />

It allows us to selectively enable/disable logs by severity and category.
But it has a caveat: once built, we couldn't modify it. So if we found a specific bug we needed to debug, we would need to create a special build with that log enabled or try debugging on the editor (but many bugs don't happen on editor).

## Overriding the Debug Log Matrix

To address this issue, we created two ways to override the Debug Log Matrix:

### Method 1: Chat Command Override

By using a chat command, we can enable/disable specific types of logs for specific categories.
The format is pretty straightforward: `/log-matrix [enable|disable] [ReportCategory] [severity]`

For example:
* `/log-matrix enable VOICE_CHAT Error`
* `/log-matrix disable SCENE_LOADING Warning`

To check the inner workings you can look at the `LogMatrixChatCommand` class.
And that's all, you will get a confirmation in the chat indicating the current state of the log (if it is enabled or disabled).

This will of course be undone if you close the client, which is why this second method exists:

### Method 2: JSON File Override

The second method involves creating a .json file that you must drop in the root folder of the build and you need to start the client with an App Argument with the name of the json file you want to open:
* For example: `--use-log-matrix "EXAMPLE_LOG_MATRIX.json"`

This allows us to have different .json files to test different features, as long as we have properly split them with ReportCategories.

The format of the json is as follows:

```json
{
  "override": true,
  "debugLogMatrix": [
    { "category": "VOICE_CHAT", "severity": "Warning" },
    { "category": "FRIENDS", "severity": "Warning" },
    { "category": "COMMUNITIES", "severity": "Warning" },
    { "category": "COMMUNITY_VOICE_CHAT", "severity": "Warning" }
  ],
  "sentryMatrix": [
    { "category": "VOICE_CHAT", "severity": "Exception" }
  ]
}
```

Where `override` indicates if we should ONLY use the values from this file or use the original values defined in the scriptable object with the values on the file overriding the original ones. As you can see we can also override the sentryMatrix, to choose which logs send to Sentry, but it is not necessarily a use case right now.

**Example Files**
* [test-log-matrix.json](https://github.com/user-attachments/files/24554325/test-log-matrix.json)
* [example-log-matrix-override.json](https://github.com/user-attachments/files/24554329/example-log-matrix-override.json)

## Use Cases

Through the .json as well as the chat command we can disable currently enabled logs, so we could potentially disable all logs except the ones we want to see, making our log files much cleaner and making errors easier to spot without dealing with issues we don't care about.

For example, if we are giving QA a feature to test, we could give them a specific .json to put with the build that would let them collect the important logs that we want, without having to modify the base Debug Log Matrix for that build.

Another use case is just for production releases, where it is even harder to change the Log Matrix.
