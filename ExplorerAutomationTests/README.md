# Explorer Automation Tests

UI automation tests for the Decentraland Explorer client using [AltTester SDK 2.3.0](https://alttester.com/docs/sdk/latest/) and NUnit.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
- [AltTester Desktop](https://alttester.com/alttester/) (Pro license required)
- An instrumented Explorer build or the Unity Editor

## Running Tests

### Automated (recommended)

Use [MetaForge](https://github.com/decentraland/metaforge) to handle the full workflow:

```bash
metaforge explorer test <PR-number-or-branch>
```

See the [Automation Testing docs](../docs/automation-testing.md) for MetaForge setup and options.

### Manual

1. **Start Explorer** with AltTester instrumentation:
   - **Build:** Launch an instrumented build with `--alttester`.
   - **Editor:** Open `AltTester > AltTester Editor`, select **Editor**, click **Play in Editor**.
2. **Start AltTester Desktop** and wait for Explorer to connect.
3. **Run the tests:**
   ```bash
   dotnet test --logger "console;verbosity=detailed"
   ```

Filter to a specific test class or test:
```bash
dotnet test --filter "ExplorePanelTests"
dotnet test --filter "TestOpenEventsFromSidebar"
```

## Project Structure

```
ExplorerAutomationTests/
├── Common/
│   └── Reporter.cs                 # Logging and Allure screenshot helpers
├── Tests/
│   ├── BaseTest.cs                 # Driver setup, view initialization, EnsureInWorld
│   ├── ExplorePanelTests.cs        # Explore panel sidebar and tab tests
│   └── ShortcutsTests.cs           # Keyboard shortcut tests
├── Views/
│   ├── BaseView.cs                 # Abstract base: click, wait, find, text helpers
│   ├── AuthenticationMainScreenView.cs
│   ├── SplashView.cs
│   ├── LoadingScreenView.cs
│   ├── MainMenuView.cs             # Sidebar buttons
│   ├── ExplorePanelView.cs         # Panel container + tab switching
│   └── ExplorePanelSections/       # Sections specific to the Explore Panel
│       ├── BaseSection.cs
│       ├── EventsSection.cs
│       ├── PlacesSection.cs
│       ├── CommunitiesSection.cs
│       ├── NavmapSection.cs
│       ├── BackpackSection.cs
│       ├── GallerySection.cs
│       └── SettingsSection.cs
└── GlobalUsings.cs
```

## Architecture

The project follows the **Page Object Model (POM)** pattern.

### View hierarchy

```
BaseView (abstract)
  ├── AuthenticationMainScreenView
  ├── SplashScreenView
  ├── LoadingScreenView
  ├── MainMenuView
  └── ExplorePanelView
        └── Sections (BaseSection)
              ├── EventsSection
              ├── PlacesSection
              ├── CommunitiesSection
              ├── NavmapSection
              ├── BackpackSection
              ├── GallerySection
              └── SettingsSection
```

- **BaseView** provides reusable interaction methods (`ClickObject`, `WaitForObject`, `WaitForObjectNotBePresent`, `IsObjectPresent`, `SetText`, `GetText`) with built-in timeout handling and Allure step tracking.
- **BaseSection** extends `BaseView` with a section locator and visibility/wait helpers. Section classes live under `Views/ExplorePanelSections/` since they are specific to the Explore Panel.
- **View classes** encapsulate UI locators as `(By, string)` tuples and expose high-level actions. Most locators use `By.ID` with UUIDs for stability.

### Test lifecycle

`BaseTest` manages the full lifecycle:

1. **OneTimeSetUp** — Connects `AltDriver` to AltTester Desktop, creates all view objects, runs `EnsureInWorld()` (waits through splash, authentication, and loading screens).
2. **SetUp** — Presses Escape to clear any open panels.
3. **Test** — Uses pre-initialized view properties (`MainMenuView`, `ExplorePanelView`, etc.).
4. **TearDown** — Takes a screenshot on failure.
5. **OneTimeTearDown** — Disconnects the driver.

### Reporting

- **Console:** Timestamped logs via `Reporter.Log()`.
- **Allure:** `[AllureStep]`, `[AllureSuite]`, `[AllureTest]` attributes generate rich HTML reports.
- **Screenshots:** Captured automatically on test failure and attached to Allure results.

## Adding a New Test

1. Create a new test class in `Tests/` that inherits from `BaseTest`.
2. Use the pre-initialized view properties — do not create new view instances in tests.
3. Add `[TestFixture]` and `[AllureSuite("...")]` attributes.

```csharp
[TestFixture]
[AllureSuite("My Feature Tests")]
public class MyFeatureTests : BaseTest
{
    [Test]
    [AllureTest("Verify something works")]
    public void TestSomethingWorks()
    {
        MainMenuView.ClickEvents();
        ExplorePanelView.WaitForPanelOpen();
        Assert.That(ExplorePanelView.Events.IsSectionVisible(), Is.True);
    }
}
```

## Adding a New View

1. Create a new class in `Views/` inheriting from `BaseView` (or `BaseSection` in `Views/ExplorePanelSections/` for Explore Panel sections).
2. Define locators as `private readonly (By, string)` tuples.
3. Add the view as a property in `BaseTest` and initialize it in `InitializeViews()`.

See [CODING_STANDARDS.md](CODING_STANDARDS.md) for detailed conventions.
