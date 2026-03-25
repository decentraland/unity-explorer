# Zendesk Support Integration

This feature integrates the [Zendesk Unity SDK](https://developer.zendesk.com/documentation/zendesk-web-widget-sdks/sdks/unity/getting_started/) into the Decentraland desktop client.

## Setup Checklist

### 1. Install the Zendesk UPM Package

The package reference in `Packages/manifest.json` uses the GitHub URL:

```json
"com.zendesk.messaging": "https://github.com/zendesk/zendesk_messaging_unity.git"
```

> **Note:** Confirm the exact URL with your Zendesk contact or the
> [Zendesk Unity SDK dependencies page](https://developer.zendesk.com/documentation/zendesk-web-widget-sdks/sdks/unity/dependencies).
> The Zendesk SDK also requires **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`), which is already present in this project.

### 2. Set the Channel Key

Replace `YOUR_ZENDESK_CHANNEL_KEY` in `ZendeskSupportController.cs` with the real channel key from your Zendesk Admin Center:
- Admin Center → Channels → Messaging → [Your channel] → Installation → Channel key

### 3. Add the Scripting Define Symbol

Once the package is installed Unity will automatically set the `ZENDESK_ENABLED` scripting define via the `versionDefines` in `ZendeskSupport.asmdef`. All Zendesk API calls are wrapped in `#if ZENDESK_ENABLED` so the project compiles cleanly even without the package.

You can verify it is active in:  
**Edit → Project Settings → Player → Other Settings → Scripting Define Symbols**

### 4. Wire Up the UI Buttons

The `SidebarView` has two new serialized button fields:

| Field | Description |
|---|---|
| `supportButton` | Opens the Zendesk Messaging UI (live chat / ticket) |
| `helpCenterButton` | Opens the Zendesk Help Center home screen |

In the Unity Editor, open the **SidebarUI** prefab and add the buttons to the sidebar layout (use the existing `SidebarButton.prefab` as a template). Then assign the button references in the `SidebarView` component.

## API Reference

```csharp
// Initialization (called automatically on first button click)
await zendeskSupportController.InitializeAsync(ct);

// Open live-chat / ticketing
zendeskSupportController.ShowMessagingAsync().Forget();

// Open Help Center home screen
zendeskSupportController.ShowHomeAsync().Forget();

// Open a specific help article
zendeskSupportController.ShowArticleAsync("https://...").Forget();
```

## Architecture

```
SidebarPlugin
  └─ ZendeskSupportController   (new – initializes SDK, exposes show/hide hooks)
  └─ SidebarController
       └─ SidebarView            (new serialized fields: supportButton, helpCenterButton)
```
