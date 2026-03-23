# Coding Standards

Conventions for writing automation tests in this project. Follow these to keep the codebase consistent.

## 1. Inherit from BaseTest

All test classes must inherit from `BaseTest`. Use the pre-initialized view properties directly — do not create view instances in tests.

```csharp
// Good
[TestFixture]
[AllureSuite("Explore Panel Tests")]
public class ExplorePanelTests : BaseTest
{
    [Test]
    public void TestOpenEventsFromSidebar()
    {
        MainMenuView.ClickEvents();
        ExplorePanelView.WaitForPanelOpen();
        Assert.That(ExplorePanelView.Events.IsSectionVisible(), Is.True);
        ExplorePanelView.ClickClose();
        ExplorePanelView.WaitForPanelClosed();
    }
}

// Bad — do not manually initialize views
[TestFixture]
public class BadTests : BaseTest
{
    private MainMenuView _menuView;

    [SetUp]
    public void TestSetUp()
    {
        _menuView = new MainMenuView(AltDriver); // Wrong: use BaseTest.MainMenuView
    }
}

// Bad — do not bypass BaseTest
[TestFixture]
public class WorseTests
{
    private AltDriver _altDriver;

    [OneTimeSetUp]
    public void Setup()
    {
        _altDriver = new AltDriver(); // Wrong: BaseTest handles driver lifecycle
    }
}
```

## 2. Use BaseView Methods

`BaseView` provides interaction methods with built-in timeout handling, error messages, and Allure step tracking. Use these instead of calling `AltDriver` directly.

**Available methods:**
- `ClickObject(locator, timeout)` — Wait for object then click
- `TapObject(locator, count, timeout)` — Multi-tap support
- `WaitForObject(locator, timeout)` — Wait for object to appear
- `WaitForObjectWhichContains(locator)` — Partial name match
- `WaitForObjectNotBePresent(locator, timeout)` — Wait for disappearance
- `IsObjectPresent(locator)` — Check presence without throwing
- `FindObject(locator)` — Direct find (throws if not found)
- `SetText(locator, text, timeout)` — Set input field text
- `GetText(locator, timeout)` — Read text content

```csharp
// Good — use BaseView methods
ClickObject(_closeButtonLocator);
WaitForObjectNotBePresent(_panelLocator, timeout: 10);

if (IsObjectPresent(_optionalButtonLocator))
    ClickObject(_optionalButtonLocator);

// Bad — direct AltDriver calls in view/test methods
var button = AltDriver.FindObject(By.ID, "some-id");
button.Click();
```

## 3. Locators

Define locators as `private readonly (By, string)` tuples. Use descriptive names ending with the element type or purpose.

Prefer `By.ID` (most stable). Use `By.NAME` when an ID is not available.

```csharp
// Good
private readonly (By, string) _panelLocator = (By.ID, "d5383a2a-d281-4fe8-b53b-fee873f32654");
private readonly (By, string) _closeButtonLocator = (By.ID, "f507113e-bb78-4ddb-9d3e-4338e1f75dfe");
private readonly (By, string) _splashScreenLocator = (By.NAME, "Splash(Clone)");

// Bad — hardcoded strings in test methods
ClickObject((By.ID, "d5383a2a-d281-4fe8-b53b-fee873f32654")); // Define as a field instead

// Bad — plain string locators
private readonly string _panelName = "ExplorePanel"; // Use tuple format
```

**Locator strategies (in order of preference):**
1. `By.ID` — UUID-based, most reliable across builds
2. `By.NAME` — GameObject name, good for well-named objects
3. `By.PATH` — Full hierarchy path, for disambiguation
4. `By.TAG` / `By.LAYER` / `By.COMPONENT` / `By.TEXT` — Use when other strategies don't fit

## 4. Waits

Use `BaseView` wait methods. Avoid `Thread.Sleep`.

```csharp
// Good
WaitForObject(_panelLocator, timeout: 10);
WaitForObjectNotBePresent(_loadingScreenLocator, timeout: 120);

// Acceptable — brief pause for animations (under 1 second)
Wait(0.5);

// Bad
Thread.Sleep(5000);

// Bad — manual polling
while (!IsObjectPresent(_panelLocator))
    Thread.Sleep(1000);
```

## 5. Page Object Model

Each distinct screen or panel gets its own view class. Keep views focused on a single responsibility.

- **Screen-level views** (e.g., `AuthenticationMainScreenView`, `LoadingScreenView`) inherit from `BaseView`.
- **Explore Panel sections** (e.g., `EventsSection`, `BackpackSection`) live in `Views/ExplorePanelSections/` and inherit from `BaseSection`. Other panels that need sections should follow the same pattern with their own subfolder (e.g., `Views/ChatPanelSections/`).
- The constructor takes an `AltDriver` (or `AltDriver` + section locator for sections).

```csharp
// View for a new panel
public class ChatPanelView : BaseView
{
    private readonly (By, string) _panelLocator = (By.ID, "chat-panel-uuid");
    private readonly (By, string) _inputFieldLocator = (By.ID, "chat-input-uuid");
    private readonly (By, string) _sendButtonLocator = (By.ID, "chat-send-uuid");

    public ChatPanelView(AltDriver altDriver) : base(altDriver) { }

    [AllureStep("Send chat message: {message}")]
    public void SendMessage(string message)
    {
        SetText(_inputFieldLocator, message);
        ClickObject(_sendButtonLocator);
    }

    public bool IsPanelVisible() => IsObjectPresent(_panelLocator);
}
```

When adding a new view, also add it as a property in `BaseTest` and initialize it in `InitializeViews()`.

## 6. Test Structure

- Use `[TestFixture]` and `[AllureSuite]` on the class.
- Use `[Test]` and `[AllureTest]` on test methods.
- Use `[TestCase]` for parameterized tests when multiple inputs share the same logic.
- Use `Reporter.Log()` for important steps.
- Keep tests independent — each test should work regardless of execution order.

```csharp
[TestFixture]
[AllureSuite("Keyboard Shortcuts")]
public class ShortcutsTests : BaseTest
{
    [Test]
    [AllureTest("Open Events panel with X shortcut")]
    public void TestOpenEventsWithShortcut()
    {
        PressKey(AltKeyCode.X);
        ExplorePanelView.WaitForPanelOpen();
        Assert.That(ExplorePanelView.Events.IsSectionVisible(), Is.True);
        PressEscape();
        ExplorePanelView.WaitForPanelClosed();
    }
}
```

## 7. Logging and Reporting

- Use `Reporter.Log()` instead of `Console.WriteLine`.
- Use `Reporter.TakeScreenshot()` for manual screenshots at important checkpoints.
- Add `[AllureStep("description")]` to view methods that represent meaningful user actions.
- Screenshots are captured automatically on test failure by `BaseTest.TearDown`.

```csharp
[AllureStep("Open events section")]
public void ClickEvents()
{
    Reporter.Log("Clicking Events button in sidebar");
    ClickObject(_eventsButtonLocator);
}
```

## 8. Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Test class | `{Feature}Tests` | `ExplorePanelTests` |
| Test method | `Test{Action}{Subject}` | `TestOpenEventsFromSidebar` |
| View class | `{Screen}View` | `MainMenuView` |
| Section class | `{Name}Section` | `EventsSection` |
| Locator field | `_{element}Locator` | `_closeButtonLocator` |
| Public view method | `{Action}{Subject}` | `WaitForPanelOpen` |
