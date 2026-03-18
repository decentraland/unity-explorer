# Automation Testing

## Overview

Automation tests use [AltTester SDK 2.3.0](https://alttester.com/docs/sdk/latest/) to drive the running application through its UI — clicking buttons, waiting for screens, and asserting on visible state. Unlike unit and integration tests, automation tests exercise the full built application as a user would.

All automation tests live in a single assembly:

- **Assembly:** `DCL.Automation.Tests`
- **Path:** `Explorer/Assets/DCL/Tests/Automation/`
- **Platforms:** Editor, Windows Standalone 64-bit

---

## How It Works

AltTester operates on a client-server model:

1. An **instrumented build** contains the AltTester prefab, which runs a WebSocket server inside the game (default: `127.0.0.1:13000`).
2. The **test code** creates an `AltDriver` that connects to this server.
3. The driver sends commands (find object, click, wait) and receives results over the WebSocket.

This means the tests run as a separate process (or in the Unity Editor) and talk to the game over the network. The game can be running locally or on a remote machine.

---

## CI Pipeline

When the `automation-tests` label is added to a PR, the CI pipeline produces an AltTester-instrumented Windows build alongside the regular builds. See [Build & CI](build-and-ci.md) for the full workflow.

The instrumented build:
- Has the `ALTTESTER` scripting define enabled
- Contains the AltTester prefab in the first scene (`Assets/Scenes/Main.unity`)
- Is built with the `Development` build option
- Is uploaded as the `Decentraland_windows64_alttester` artifact

---

## Prerequisites

You need **[AltTester Desktop](https://alttester.com/alttester/)** installed and running before executing any automation tests. AltTester Desktop acts as the bridge between the test runner and the instrumented application.

---

## Running Tests

### Step 1: Start the Instrumented Application

You have two options:

#### Option A: Run an Instrumented Build

Download and launch the `Decentraland_windows64_alttester` build artifact from your PR (the download link is posted as a PR comment when the `automation-tests` label is present). You should see the AltTester connection overlay in the application.

#### Option B: Run from the Unity Editor

1. Open the AltTester Editor window: `AltTester > AltTester Editor`
2. Make sure **Editor** is selected as the platform.
3. Click **Run** in the AltTester window — this enters Play Mode with the AltTester server active.

> **Note:** When running in the Editor, the `ALTTESTER` scripting define must be set. The project has `KeepAUTSymbolDefined: 1` in `AltTesterEditorSettings.asset`, so the define persists across Editor sessions on this branch.

### Step 2: Run Tests from AltTester Desktop

1. Make sure **AltTester Desktop** is running and connected to the instrumented application.
2. In the AltTester Editor window, select the `DCL.Automation.Tests` assembly.
3. Make sure that either **Editor** or **Standalone** is selected, depending on where you want to run the tests.
3. Click **Run All Tests**, or expand the assembly and select individual tests to run.
