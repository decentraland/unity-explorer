# Coding Standards and Preferences

This document outlines the coding standards and preferences for the AltTester Unity/Unreal Engine test automation framework using C# and NUnit. These guidelines should be followed to maintain consistency, readability, and reliability across the codebase.

## 1. Test Setup - Inherit from BaseTest and Use Pre-Initialized Views

### ✅ Preferred Approach
```csharp
[TestFixture]
[AllureSuite("Game Feature Tests")]
public class GameFeatureTests : BaseTest
{
    [Test]
    public void TestGameFeature()
    {
        // Use the pre-initialized view objects directly from BaseTest
        MainMenuView.WaitForMainMenuReady();
        MainMenuView.StartNewGame("TestPlayer");
        
        GamePlayView.WaitForGamePlayReady();
        Assert.That(GamePlayView.IsMainCharacterPresent(), Is.True, 
            "Main character should be present after starting a new game");
    }

    [Test]
    public void TestAnotherFeature()
    {
        // All view instances are automatically available
        MainMenuView.NavigateToSettings();
        // Test implementation...
    }
}
```

### ❌ Avoid
```csharp
[TestFixture]
public class GameFeatureTests : BaseTest
{
    private MainMenuView mainMenuView;
    private GamePlayView gamePlayView;

    [SetUp]
    public void TestSetUp()
    {
        // Don't manually initialize view objects - they're already available from BaseTest
        mainMenuView = new MainMenuView(Drivers);
        gamePlayView = new GamePlayView(Drivers);
    }

    [Test]
    public void TestGameFeature()
    {
        // Using local instances instead of BaseTest properties
        mainMenuView.StartNewGame("TestPlayer");
    }
}

// Also avoid: Direct driver usage without view abstraction
[TestFixture]
public class BadGameFeatureTests
{
    private AltDriver altDriver;
    
    [OneTimeSetUp]
    public void Setup()
    {
        // Duplicating driver setup logic from BaseTest
        altDriver = new AltDriver("127.0.0.1", 13000, "MyGame");
    }
    
    [Test]
    public void TestGameFeature()
    {
        // Direct driver usage without view abstraction
        var button = altDriver.FindObject(By.NAME, "PlayButton");
        button.Click();
    }
}
```

**Guidelines:**
- Always inherit from `BaseTest` for consistent driver and view setup
- Use the pre-initialized view properties directly: `MainMenuView`, `GamePlayView`
- No need for `[SetUp]` methods to initialize views - they're automatically available
- BaseTest handles all driver setup, teardown, and view initialization
- Leverage the automatic screenshot and logging functionality from BaseTest

## 2. Method Reuse - Leverage Existing BaseView Methods

### ✅ Preferred Approach
```csharp
// Use existing methods from BaseView class
ClickObject(PlayButtonLocator);
var gameObject = WaitForObject(MainCharacterLocator, timeout: 5.0f);
var isPresent = IsObjectPresent(HealthBarLocator);
SetText(PlayerNameInputLocator, "TestPlayer");
var healthText = GetText(HealthDisplayLocator);
```

### ❌ Avoid
```csharp
// Creating new methods when BaseView methods exist
public void CustomClickMethod(By locator, string value)
{
    var altObject = AltDriver.FindObject(locator, value);
    altObject.Click();
}

public void CustomWaitMethod(By locator, string value)
{
    // Reimplementing wait logic that already exists in BaseView
}
```

**Guidelines:**
- Always check `BaseView` class for existing functionality
- Only create new methods when BaseView methods don't meet specific requirements
- Extend BaseView functionality rather than reimplementing
- Use the standardized locator tuple format: `(By, string)`

## 3. Locators - Use Tuple Format with AltTester By Strategies

### ✅ Preferred Approach
```csharp
using By = AltTester.AltTesterUnitySDK.Driver.By;

public class MainMenuView : BaseView
{
    // Define locators as tuples of (By, string)
    private readonly (By, string) PlayButtonLocator = (By.NAME, "PlayButton");
    private readonly (By, string) SettingsButtonLocator = (By.NAME, "SettingsButton");
    private readonly (By, string) MainCharacterLocator = (By.PATH, "//Player/Character");
    private readonly (By, string) HealthTextLocator = (By.COMPONENT, "Text");
    private readonly (By, string) UICanvasLocator = (By.TAG, "UI");
    private readonly (By, string) ScoreDisplayLocator = (By.ID, "123");

    // Usage in methods
    public void ClickPlayButton()
    {
        ClickObject(PlayButtonLocator);
    }
    
    public bool IsMainMenuVisible()
    {
        return IsObjectPresent(MainMenuPanelLocator);
    }
}
```

### ❌ Avoid
```csharp
// String locators that require By specification every time
private readonly string PlayButtonName = "PlayButton";
private readonly string SettingsButtonPath = "//Canvas/MainMenu/SettingsButton";

// Usage requiring By specification in every call
var playButton = AltDriver.FindObject(By.NAME, PlayButtonName);
var settingsButton = AltDriver.FindObject(By.PATH, SettingsButtonPath);

// Hardcoded locators in test methods
ClickObject((By.NAME, "PlayButton")); // Should be defined as constant
```

**AltTester Locator Strategies:**
- `By.NAME` - Find by GameObject/Actor name
- `By.PATH` - Find by GameObject/Actor path (most reliable)
- `By.ID` - Find by GameObject/Actor instance ID
- `By.TAG` - Find by GameObject/Actor tag
- `By.LAYER` - Find by GameObject/Actor layer
- `By.COMPONENT` - Find by attached component type
- `By.TEXT` - Find by text content (for UI text elements)

**Guidelines:**
- Always define locators as tuples: `(By.STRATEGY, "selector")`
- Use descriptive names that indicate the element's purpose
- Group related locators together in the view class
- Prefer `By.PATH` for complex hierarchies and `By.NAME` for unique objects
- Use `By.COMPONENT` when you need to find objects by their component type

## 4. Element Interaction - Use BaseView Wrapper Methods

### ✅ Preferred Approach
```csharp
// Use BaseView wait methods with built-in timeout handling
var gameObject = WaitForObject(MainCharacterLocator, timeout: 10.0f);
var menuPanel = WaitForObjectWhichContains((By.NAME, "Menu"), timeout: 5.0f);
WaitForObjectNotBePresent(LoadingScreenLocator, timeout: 30.0f);

// Check object presence without throwing exceptions
if (IsObjectPresent(OptionalButtonLocator))
{
    ClickObject(OptionalButtonLocator);
}

// Use the Wait method for intentional delays (sparingly)
Wait(0.5); // Brief pause for UI animations
```

### ❌ Avoid
```csharp
// Direct AltDriver wait methods without error handling
var gameObject = AltDriver.WaitForObject(By.NAME, "Character", timeout: 10.0f);

// Thread.Sleep for waiting on game state
Thread.Sleep(5000); // Avoid fixed delays

// Polling loops
bool found = false;
int attempts = 0;
while (!found && attempts < 10)
{
    try
    {
        AltDriver.FindObject(By.NAME, "Character");
        found = true;
    }
    catch
    {
        Thread.Sleep(1000);
        attempts++;
    }
}
```

**Guidelines:**
- Use `WaitForObject()` methods from BaseView for reliable object waiting
- Use `IsObjectPresent()` for conditional logic that doesn't need exceptions
- Only use `Wait()` method for brief UI synchronization (< 1 second)
- Avoid `Thread.Sleep()` except for very short UI animation waits
- Trust the timeout mechanisms in BaseView methods

## 5. Element Interaction - Use BaseView Wrapper Methods

### ✅ Preferred Approach
```csharp
// Use BaseView wrapper methods with tuple locators
ClickObject(PlayButtonLocator);
TapObject(MenuButtonLocator, count: 2);
SetText(PlayerNameInputLocator, "TestPlayer");
var healthValue = GetText(HealthDisplayLocator);
var currentScene = GetCurrentScene();

// Use proper locator definitions
private readonly (By, string) PlayButtonLocator = (By.NAME, "PlayButton");
private readonly (By, string) HealthBarLocator = (By.PATH, "//Canvas/HealthPanel/HealthBar");
private readonly (By, string) ScoreTextLocator = (By.COMPONENT, "Text");
```

### ❌ Avoid
```csharp
// Direct AltDriver methods
var playButton = AltDriver.FindObject(By.NAME, "PlayButton");
playButton.Click();

// String-based locators
private readonly string PlayButtonName = "PlayButton";
var playButton = AltDriver.FindObject(By.NAME, PlayButtonName);

// Hardcoded locator values in test methods
var button = AltDriver.FindObject(By.NAME, "PlayButton");
```

**Available BaseView Methods:**
- `ClickObject((By, string) locator, float timeout = 10.0f, bool waitForClick = true)`
- `TapObject((By, string) locator, int count = 1, float timeout = 10.0f)`
- `WaitForObject((By, string) locator, float timeout = 20.0f)`
- `SetText((By, string) locator, string text, float timeout = 10.0f)`
- `GetText((By, string) locator, float timeout = 10.0f)`
- `IsObjectPresent((By, string) locator)`
- `FindObject((By, string) locator)`

**Benefits:**
- Consistent error handling with meaningful exception messages
- Built-in timeout handling and retries
- Automatic screenshot capture on failures
- Standardized logging with Allure integration

## 5. Waits - Use BaseView Wait Methods, Avoid Thread.Sleep

### ✅ Preferred Approach
```csharp
// Use BaseView wait methods with built-in timeout handling
var gameObject = WaitForObject(MainCharacterLocator, timeout: 10.0f);
var menuPanel = WaitForObjectWhichContains((By.NAME, "Menu"), timeout: 5.0f);
WaitForObjectNotBePresent(LoadingScreenLocator, timeout: 30.0f);

// Check object presence without throwing exceptions
if (IsObjectPresent(OptionalButtonLocator))
{
    ClickObject(OptionalButtonLocator);
}

// Use the Wait method for intentional delays (sparingly)
Wait(0.5); // Brief pause for UI animations
```

### ❌ Avoid
```csharp
// Direct AltDriver wait methods without error handling
var gameObject = AltDriver.WaitForObject(By.NAME, "Character", timeout: 10.0f);

// Thread.Sleep for waiting on game state
Thread.Sleep(5000); // Avoid fixed delays

// Polling loops
bool found = false;
int attempts = 0;
while (!found && attempts < 10)
{
    try
    {
        AltDriver.FindObject(By.NAME, "Character");
        found = true;
    }
    catch
    {
        Thread.Sleep(1000);
        attempts++;
    }
}
```

**Guidelines:**
- Use `WaitForObject()` methods from BaseView for reliable object waiting
- Use `IsObjectPresent()` for conditional logic that doesn't need exceptions
- Only use `Wait()` method for brief UI synchronization (< 1 second)
- Avoid `Thread.Sleep()` except for very short UI animation waits
- Trust the timeout mechanisms in BaseView methods

## 6. Page Object Model (POM) Architecture - Create View Classes for Each Game Screen

### ✅ Preferred Approach
```csharp
// Create separate view classes for each distinct game screen or UI panel
public class InventoryView : BaseView
{
    private readonly (By, string) InventoryPanelLocator = (By.NAME, "InventoryPanel");
    private readonly (By, string) ItemSlotLocator = (By.COMPONENT, "InventorySlot");
    private readonly (By, string) CloseButtonLocator = (By.PATH, "//InventoryPanel/CloseButton");

    public InventoryView(DriverContainer drivers) : base(drivers)
    {
    }

    [AllureStep("Open inventory panel")]
    public void OpenInventory()
    {
        // Implementation specific to inventory functionality
    }

    [AllureStep("Get item count in inventory")]
    public int GetItemCount()
    {
        // Inventory-specific logic
        return FindObjects(ItemSlotLocator).Count;
    }
}

public class SettingsView : BaseView
{
    private readonly (By, string) SettingsPanelLocator = (By.NAME, "SettingsPanel");
    private readonly (By, string) VolumeSliderLocator = (By.NAME, "VolumeSlider");
    private readonly (By, string) ApplyButtonLocator = (By.NAME, "ApplyButton");

    public SettingsView(DriverContainer drivers) : base(drivers)
    {
    }

    [AllureStep("Adjust volume setting")]
    public void SetVolume(float volume)
    {
        // Settings-specific functionality
    }
}

// Don't forget to add new views to BaseTest for automatic initialization
public class BaseTest
{
    protected MainMenuView MainMenuView { get; set; }
    protected GamePlayView GamePlayView { get; set; }
    protected InventoryView InventoryView { get; set; }  // Add new view
    protected SettingsView SettingsView { get; set; }    // Add new view

    public void InitializeViews()
    {
        MainMenuView = new MainMenuView(Drivers);
        GamePlayView = new GamePlayView(Drivers);
        InventoryView = new InventoryView(Drivers);       // Initialize new view
        SettingsView = new SettingsView(Drivers);         // Initialize new view
    }
}
```

### ❌ Avoid
```csharp
// Putting all functionality in one massive view class
public class GameView : BaseView
{
    // Main menu locators
    private readonly (By, string) PlayButtonLocator = (By.NAME, "PlayButton");
    
    // Inventory locators  
    private readonly (By, string) InventoryPanelLocator = (By.NAME, "InventoryPanel");
    
    // Settings locators
    private readonly (By, string) SettingsPanelLocator = (By.NAME, "SettingsPanel");
    
    // Gameplay locators
    private readonly (By, string) MainCharacterLocator = (By.NAME, "MainCharacter");

    // Mixed responsibilities - violates single responsibility principle
    public void StartNewGame() { /* Main menu functionality */ }
    public void OpenInventory() { /* Inventory functionality */ }
    public void ChangeSettings() { /* Settings functionality */ }
    public void MoveCharacter() { /* Gameplay functionality */ }
}

// Using test methods without proper view abstraction
[Test]
public void TestInventoryFeature()
{
    // Direct driver usage instead of view methods
    var inventoryButton = Drivers.AltDriver.FindObject(By.NAME, "InventoryButton");
    inventoryButton.Click();
    
    var items = Drivers.AltDriver.FindObjects(By.COMPONENT, "InventorySlot");
    Assert.That(items.Count, Is.GreaterThan(0));
}
```

**POM Principles for View Creation:**

1. **Single Responsibility**: Each view class should represent one distinct game screen, UI panel, or functional area
2. **Encapsulation**: All locators and interactions for a specific screen should be contained within its view class
3. **Abstraction**: View methods should provide meaningful, high-level actions rather than exposing low-level driver operations
4. **Reusability**: View methods should be designed to be reusable across multiple test scenarios
5. **Maintainability**: Changes to a specific game screen should only require updates to its corresponding view class

**When to Create New View Classes:**
- **New Game Screens**: Main menu, gameplay, pause menu, game over screen
- **UI Panels**: Inventory, settings, character selection, shop/store
- **Dialog/Modal Windows**: Confirmation dialogs, error messages, tutorials
- **HUD Elements**: If complex enough, create separate views for different HUD sections
- **Mini-Games**: Separate views for distinct mini-game interfaces within the main game

**Naming Conventions:**
- Use descriptive names that clearly indicate the game screen: `MainMenuView`, `InventoryView`, `SettingsView`
- End all view class names with "View" for consistency
- Use the same name as the Unity scene, Unreal level, or main GameObject/Actor when possible

**Integration with BaseTest:**
- Always add new view properties to BaseTest for automatic initialization
- Update the `InitializeViews()` method to instantiate new view classes
- This ensures all views are available immediately in test classes without manual setup

## 7. Test Structure - Use NUnit Parameterized Tests

### ✅ Preferred Approach
```csharp
[TestCase("Character", "StartPosition", TestName = "TestCharacterAtStartPosition")]
[TestCase("Enemy", "SpawnPoint", TestName = "TestEnemyAtSpawnPoint")]
[TestCase("Collectible", "RandomPosition", TestName = "TestCollectibleAtRandomPosition")]
public void TestGameObjectsAtPositions(string objectType, string positionType)
{
    // Single test method that handles multiple object types
    var gameObject = gamePlayView.FindGameObject(objectType);
    var expectedPosition = gamePlayView.GetPosition(positionType);
    
    Assert.That(gameObject.GetWorldPosition(), Is.EqualTo(expectedPosition).Within(0.1f),
        $"{objectType} should be at {positionType}");
}

// Alternative using TestCaseSource for complex data
private static readonly object[] GameObjectTestCases = 
{
    new object[] { "Player", new { Health = 100, Level = 1 } },
    new object[] { "Boss", new { Health = 500, Level = 10 } },
    new object[] { "NPC", new { Health = 50, Level = 5 } }
};

[TestCaseSource(nameof(GameObjectTestCases))]
public void TestGameObjectProperties(string objectName, object expectedProperties)
{
    // Test implementation using anonymous object properties
}
```

### ❌ Avoid
```csharp
[Test]
public void TestCharacterAtStartPosition()
{
    // Separate method for each object type
}

[Test]
public void TestEnemyAtSpawnPoint()
{
    // Separate method for each object type
}

[Test]
public void TestCollectibleAtRandomPosition()
{
    // Separate method for each object type
}
```

**Benefits:**
- Reduces code duplication
- Easier maintenance
- Consistent test structure
- Better coverage visibility with descriptive test names

## 8. Additional Best Practices

### View Organization and Architecture
- Keep each view class focused on a single game screen or UI panel
- Use descriptive class names that match the Unity scene, Unreal level, or UI panel: `MainMenuView`, `GamePlayView`, `InventoryView`
- Group related methods together within view classes
- Inherit from `BaseView` to get standard interaction methods

### Method Design
- Use descriptive method names that indicate the action being performed: `StartNewGame()`, `NavigateToSettings()`, `WaitForGamePlayReady()`
- Keep methods focused on single responsibilities
- Use `[AllureStep]` attributes for better test reporting
- Return meaningful values when methods need to provide information

### Error Handling and Assertions
- Use NUnit assertions with descriptive failure messages
- Leverage BaseView exception handling for consistent error reporting
- Provide context in assertion messages to help with debugging

### Test Organization
- Use `[TestFixture]` and `[AllureSuite]` attributes for better organization
- Group related tests in the same test class
- Use meaningful test method names that describe the expected behavior
- Use `[SetUp]` and `[TearDown]` for test-specific initialization and cleanup

### Logging and Reporting
- Use `Reporter.Log()` for important test steps and debugging information
- Add `withScreenshot: true` parameter when logging errors or important states
- Use `[AllureStep]` attributes consistently across view methods

### Code Documentation
- Prefer good variable and method names over extensive comments
- Document complex game interactions or timing-sensitive operations

### Example: Complete Implementation Following Standards

```csharp
using By = AltTester.AltTesterUnitySDK.Driver.By;

namespace AltTesterProject.Views
{
    public class InventoryView : BaseView
    {
        // Locator definitions using tuple format
        private readonly (By, string) InventoryPanelLocator = (By.NAME, "InventoryPanel");
        private readonly (By, string) CloseButtonLocator = (By.PATH, "//InventoryPanel/CloseButton");
        private readonly (By, string) ItemSlotLocator = (By.COMPONENT, "InventorySlot");
        
        public InventoryView(DriverContainer drivers) : base(drivers)
        {
        }


        [AllureStep("Wait for inventory to be ready")]
        public void WaitForInventoryReady(int timeoutSeconds = 10)
        {
            WaitForObject(InventoryPanelLocator, timeoutSeconds);
            Reporter.Log("Inventory panel is ready for interaction");
        }

        [AllureStep("Close inventory panel")]
        public void CloseInventory()
        {
            if (!IsObjectPresent(InventoryPanelLocator))
            {
                Reporter.Log("Inventory panel is not currently open");
                return;
            }

            ClickObject(CloseButtonLocator);
            WaitForObjectNotBePresent(InventoryPanelLocator, timeout: 5.0f);
            Reporter.Log("Inventory panel closed successfully");
        }

        [AllureStep("Get inventory item count")]
        public int GetInventoryItemCount()
        {
            try
            {
                var itemSlots = AltDriver.FindObjects(ItemSlotLocator.Item1, ItemSlotLocator.Item2);
                var itemCount = itemSlots.Where(slot => !string.IsNullOrEmpty(slot.GetText())).Count();
                Reporter.Log($"Found {itemCount} items in inventory");
                return itemCount;
            }
            catch (Exception ex)
            {
                Reporter.Log($"Error getting inventory item count: {ex.Message}", withScreenshot: true);
                return 0;
            }
        }
    }
}

[TestFixture]
[AllureSuite("Inventory Management Tests")]
public class InventoryTests : BaseTest
{
    [TestCase("Sword", 1, TestName = "TestCollectSword")]
    [TestCase("Shield", 1, TestName = "TestCollectShield")]
    [TestCase("Potion", 3, TestName = "TestCollectMultiplePotions")]
    public void TestCollectItems(string itemType, int expectedCount)
    {
        // Setup: Start game and navigate to gameplay using pre-initialized views
        MainMenuView.WaitForMainMenuReady();
        MainMenuView.StartNewGame("TestPlayer");
        GamePlayView.WaitForGamePlayReady();

        // Action: Collect the specified items
        for (int i = 0; i < expectedCount; i++)
        {
            GamePlayView.CollectItem(itemType);
        }

        // Verification: Check inventory contains expected items
        GamePlayView.OpenInventory();
        // Note: If you had an InventoryView, it would also be pre-initialized in BaseTest
        
        var itemCount = GamePlayView.GetInventoryItemCount(); // Assuming this method exists
        Assert.That(itemCount, Is.GreaterThanOrEqualTo(expectedCount), 
            $"Should have collected at least {expectedCount} {itemType}(s)");
    }
}
```


Always refer to this document when writing new tests or refactoring existing code to maintain high code quality standards specific to Unity/Unreal Engine game testing with AltTester.