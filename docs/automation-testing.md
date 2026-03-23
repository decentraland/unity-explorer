# Automation Testing

## Overview

Automation tests use [AltTester SDK 2.3.0](https://alttester.com/docs/sdk/latest/) to drive the running application through its UI — clicking buttons, waiting for screens, and asserting on visible state. Unlike unit and integration tests, automation tests exercise the full built application as a user would.

The tests live in a standalone .NET test project, separate from the Unity project:

- **Project:** `ExplorerAutomationTests/`
- **Framework:** .NET 8.0, NUnit 3, Allure reporting
- **Driver:** AltTester-Driver 2.3.0

---

## How It Works

AltTester operates on a client-server model:

1. An **instrumented build** contains the AltTester prefab, which connects to AltTester Desktop (default: `127.0.0.1:13000`).
2. **AltTester Desktop** acts as the bridge server between the application and the tests.
3. The **test code** creates an `AltDriver` that connects to this server.
4. The driver sends commands (find object, click, wait) and receives results over WebSocket.

This means the tests run as a separate process and talk to the game over the network. The game can be running locally or on a remote machine.

---

## Test Architecture

The project follows the **Page Object Model (POM)** pattern with a view hierarchy:

```
ExplorerAutomationTests/
├── Common/
│   └── Reporter.cs                 # Timestamped logging and Allure step/screenshot helpers
├── Tests/
│   ├── BaseTest.cs                 # Base class: driver setup, view init, EnsureInWorld flow
│   ├── ExplorePanelTests.cs        # Explore panel sidebar and tab tests
│   └── ShortcutsTests.cs           # Keyboard shortcut tests
├── Views/
│   ├── BaseView.cs                 # Abstract base: click, wait, find, text helpers
│   ├── AuthenticationMainScreenView.cs
│   ├── SplashView.cs
│   ├── LoadingScreenView.cs
│   ├── MainMenuView.cs             # Sidebar buttons (events, places, map, etc.)
│   ├── ExplorePanelView.cs         # Panel tabs + section view instances
│   └── ExplorePanelSections/       # Sections specific to the Explore Panel
│       ├── BaseSection.cs          # Abstract base for panel sections
│       ├── EventsSection.cs
│       ├── PlacesSection.cs
│       ├── CommunitiesSection.cs
│       ├── NavmapSection.cs
│       ├── BackpackSection.cs
│       ├── GallerySection.cs
│       └── SettingsSection.cs
├── GlobalUsings.cs
└── ExplorerAutomationTests.csproj
```

### Key patterns

- **BaseTest** connects the `AltDriver`, initializes all view objects, and runs `EnsureInWorld()` (handles splash screen, authentication, and loading).
- **View classes** encapsulate locators (as `(By, string)` tuples) and interaction methods. Most locators use `By.ID` with UUIDs for stability.
- **BaseView** provides reusable methods: `ClickObject`, `WaitForObject`, `WaitForObjectNotBePresent`, `IsObjectPresent`, `SetText`, `GetText`.
- **Reporter** wraps console logging with timestamps and creates Allure steps/screenshots.
- **Allure** attributes (`[AllureSuite]`, `[AllureTest]`, `[AllureStep]`) decorate tests and view methods for rich HTML reports.

### Test flow

```
OneTimeSetUp:
  StartDriver()        → Connect AltDriver to AltTester Desktop
  InitializeViews()    → Create all view/section objects
  EnsureInWorld()      → Wait through splash → auth → loading

Per-test SetUp:
  PressEscape()        → Clear any open panels

Test method:
  Uses pre-initialized views (MainMenuView, ExplorePanelView, etc.)

Per-test TearDown:
  Screenshot on failure

OneTimeTearDown:
  Stop AltDriver
```

---

## CI Pipeline

Non-release builds created by CI (for PRs and the dev branch) include AltTester instrumentation. The instrumented build:
- Has the `ALTTESTER` scripting define enabled
- Accepts the `--alttester` launch argument to activate instrumentation at runtime. This loads the AltTester prefab on start.

---

## Running Tests

### Option 1: Automated with MetaForge (Recommended)

[MetaForge](https://github.com/decentraland/metaforge) is our CLI tool that automates the entire test workflow.

#### Prerequisites

1. Install MetaForge and update to the latest release.
2. Install [AltTester Desktop](https://alttester.com/alttester/) (You need a Pro license / trial)
3. Set the AltTester license key in MetaForge:
   ```bash
   metaforge explorer test --set-license <your-license-key>
   ```
4. Make sure [Node.js](https://nodejs.org/) is installed (needed for Allure report generation).
5. Make sure you are logged in to Explorer.

#### Running

```bash
metaforge explorer test <PR-number-or-branch>
```

For example, to test PR #7645:

```bash
metaforge explorer test 7645
```

This will:
1. Download the instrumented build from the PR.
2. Launch it with `--alttester` to enable instrumentation.
3. Start AltTester Desktop in batch mode, activate the license, and wait for Explorer to connect.
4. Clone the `ExplorerAutomationTests` folder from the explorer repo on the PR's branch.
5. Run all tests via `dotnet test`.
6. Generate an Allure HTML report and open it in the browser.
7. Deactivate the AltTester license on completion (for easier license sharing).

#### MetaForge options

| Option | Description |
|---|---|
| `--filter <expr>` | NUnit filter expression (e.g. `--filter "Category=Smoke"`) |
| `--timeout <seconds>` | AltTester server startup timeout (default: 120) |
| `--set-license <key>` | Store AltTester license key and exit |
| `--deactivate` | Deactivate the current AltTester license and exit |
| `--skip-allure-open` | Generate report but don't open it in the browser |

---

### Option 2: Manual

Manual testing requires three things running: an instrumented Explorer instance, AltTester Desktop, and the test runner.

#### Step 1: Start the instrumented application

**From a CI build:**
Download an instrumented build from a PR or the dev branch (non-release builds have AltTester support). Launch it with the `--alttester` argument:

```bash
./Decentraland --alttester
```

**From the Unity Editor:**
1. Open `AltTester > AltTester Editor`.
2. Select **Editor** as the platform.
3. Click **Play in Editor** — this enters Play Mode with the AltTester server active.

> **Note:** When running in the Editor, the `ALTTESTER` scripting define must be set. The project has `KeepAUTSymbolDefined: 1` in `AltTesterEditorSettings.asset`, so the define persists across Editor sessions.

#### Step 2: Start AltTester Desktop

Launch AltTester Desktop. You should see Explorer connect in the AltTester Desktop UI.

#### Step 3: Run the tests

```bash
cd ExplorerAutomationTests
dotnet test --logger "console;verbosity=detailed"
```

To run a specific test class:
```bash
dotnet test --filter "ExplorePanelTests"
```

To run a single test:
```bash
dotnet test --filter "TestOpenEventsFromSidebar"
```

---

## AltTester AI Extension

AltTester Desktop includes an AI extension that lets us generate test code using Claude Code. The extension works as an [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server, giving Claude real-time access to the running game's object hierarchy.

### Prerequisites

- AltTester Desktop **v2.2.7 or later**
- A **Pro license** (all current Pro licenses include AI extension support)
- [Claude Code](https://docs.anthropic.com/en/docs/claude-code) CLI installed

### Installation

On first launch with a qualifying license, AltTester Desktop prompts to install the extension. Click **Download** to install it automatically.

Alternatively, open AltTester Desktop **Settings** and click **Configure AltTester AI Extension > Open Configuration Setup**.

### Configuring Claude Code

After installing the extension, add the AltTester MCP server to your Claude Code configuration. Add the following to your `.claude/settings.json` (project-level) or `~/.claude/settings.json` (global):

```json
{
  "mcpServers": {
    "alttester": {
      "command": "/path/to/AltTesterMcp"
    }
  }
}
```

- **macOS/Linux:** Use the path from the AltTester Data Path directory (e.g. `"/Users/<user>/Library/Application Support/AltTesterDesktop/AltTesterMcp"`).
- **Windows:** Use the `.exe` path (e.g. `"C:\\Users\\<user>\\AppData\\Local\\AltTesterDesktop\\AltTesterMcp.exe"`).

Restart Claude Code after updating the configuration.

### Usage

With the MCP server configured and AltTester Desktop connected to the running game, Claude can:

- **Inspect the live game** — query the object hierarchy, read properties, and understand the current UI state.
- **Generate test code** — write C# tests following this project's patterns and coding standards.
- **Debug failing tests** — analyze failures and suggest alternative locators or debugging strategies.

Example prompts:
- *"Help me write a test that opens the Backpack panel and verifies it's visible."*
- *"What objects are currently visible on screen?"*
- *"Why is my test failing to find the Settings button? Suggest alternative locators."*

For full documentation, see the [AltTester AI Extension docs](https://alttester.com/docs/desktop/latest/pages/ai-extension.html).
