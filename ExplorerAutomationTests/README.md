# AltTester C# Test Project - Getting Started Guide

## 🎯 What is this?

This is a **beginner-friendly** test automation project for Unity games using AltTester. If you're new to automated testing, don't worry! This guide will walk you through everything step-by-step.

**What you'll learn:**
- How to automatically test your Unity game (no manual clicking!)
- How to find and interact with game objects programmatically
- How to verify your game works correctly across different scenarios

## ⚡ Quick Start (5 minutes to first test!)

**The fastest way to get started:**

1. **Make sure your Unity game is running** with AltTester already connected
2. **Run the test script:**
   - **macOS/Linux:** `./run_tests.sh`
   - **Windows:** `run_tests.bat`
3. **Watch the magic happen!** ✨

The template includes example tests that you can adapt for any game!

## � Complete Beginner Setup

### Step 1: What You Need Before Starting

Before we begin, you need:

1. **A Unity game with AltTester integrated** (this should already be done by your development team)
2. **AltTester Desktop app** - Download from [altom.com/alttester](https://altom.com/alttester/)
3. **.NET 8.0 SDK** - Download from [microsoft.com/net](https://dotnet.microsoft.com/download)

### Step 2: Get AltTester Desktop Running

1. **Download and install AltTester Desktop**
2. **Launch AltTester Desktop**
3. **Start your Unity game** (the one with AltTester integration)
4. **Verify connection:** You should see your game appear in AltTester Desktop

⚠️ **Important:** Your game must be connected to AltTester Desktop before running tests!

### Step 3: Prepare Your Test Project

1. **Download/clone this project** to your computer
2. **Open a terminal/command prompt** in the project folder
3. **Install dependencies:**
   ```bash
   dotnet restore
   ```
4. **Build the project:**
   ```bash
   dotnet build
   ```

### Step 4: Run Your First Test

**Option A: Use the Simple Scripts (Recommended for beginners)**

- **macOS/Linux:**
  ```bash
  ./run_tests.sh
  ```
- **Windows:**
  ```cmd
  run_tests.bat
  ```

**Option B: Use dotnet CLI (More control)**
```bash
dotnet test --logger "console;verbosity=detailed"
```

🎉 **Congratulations!** You just ran your first automated test!

## 🚀 Features

- **View Object Model**: Clean separation of test logic and view interactions
- **Multi-Driver Support**: AltTester, Appium (mobile), and Selenium (web) integration
- **Two Template Views**: Main menu and gameplay view objects to get you started quickly
- **Comprehensive Examples**: Template code covering common game testing scenarios
- **NUnit Framework**: Industry-standard testing framework for .NET
- **Configuration Management**: Environment-based configuration with sensible defaults
- **Utility Classes**: Common helpers for locators, reporting, and driver management
- **Easy Customization**: Template code with clear instructions for adaptation

## 📁 Project Structure

```
AltTesterProject/
├── Common/                    # Shared utilities and infrastructure
│   ├── DriverContainer.cs     # Multi-driver container
│   ├── Reporter.cs           # Test reporting utilities
│   └── TestConfiguration.cs  # Configuration management
├── Views/                     # View Object Model classes
│   ├── BaseView.cs           # Base view with common functionality
│   ├── MainMenuView.cs       # Main menu view object
│   └── GamePlayView.cs       # Game play view object
├── Tests/                     # Test classes
│   ├── BaseTest.cs           # Base test class with setup/teardown
│   └── MainMenuTests.cs      # Main menu tests
├── run_tests.sh              # Simple test runner for Linux/macOS
├── run_tests.bat             # Simple test runner for Windows
└── GlobalUsings.cs           # Global using statements
```

## 🛠️ Setup Options

### Option 1: Local Testing (Easiest - Recommended for Beginners)

This is the **simplest way** to get started. Your Unity game runs on the same computer as your tests.

**Requirements:**
- Unity game with AltTester integration running locally
- AltTester Desktop app connected to your game

**How to run:**
```bash
# macOS/Linux
./run_tests.sh

# Windows
run_tests.bat
```

### Option 2: Mobile Testing with Appium (Advanced)

Use this when you want to test your game on mobile devices (Android/iOS).

**Additional Requirements:**
- **Appium Server** installed and running
- **Android SDK/Xcode** (depending on platform)
- **Physical device or emulator** connected

**Setup Appium (if you're new to this):**

1. **Install Appium:**
   ```bash
   npm install -g appium
   ```

2. **Start Appium Server:**
   ```bash
   appium
   ```

3. **Run tests with Appium:**
   ```bash
   # macOS/Linux
   RUN_TESTS_WITH_APPIUM=true ./run_tests.sh
   
   # Windows
   set RUN_TESTS_WITH_APPIUM=true && run_tests.bat
   ```

### Option 3: WebGL Testing with Selenium (Advanced)

Use this when your game is deployed as WebGL and runs in a browser.

**Additional Requirements:**
- **Chrome browser** installed
- **ChromeDriver** (automatically managed)

**Run WebGL tests:**
```bash
# macOS/Linux
TEST_PLATFORM=WebGL RUN_TESTS_WITH_SELENIUM=true ./run_tests.sh

# Windows  
set TEST_PLATFORM=WebGL && set RUN_TESTS_WITH_SELENIUM=true && run_tests.bat
```

## 🎮 Understanding the Code (For Beginners)

### What are "Views"?

Think of Views as **representatives** of your game screens. Instead of manually clicking buttons, Views do it programmatically.

**Example:** `MainMenuView.cs` represents your game's main menu and can:
- Click the "Play" button
- Check if the menu loaded correctly
- Navigate to other screens

### What are "Tests"?

Tests are **instructions** that tell the computer how to verify your game works correctly.

**Example:** A test might:
1. Start the game
2. Check if the main menu appears
3. Click "Play"
4. Verify the game starts correctly

### Project Structure Explained

```
AltTesterProject/
├── Views/                     # 🎭 Game screen representatives
│   ├── MainMenuView.cs       #    - Handles main menu interactions
│   └── GamePlayView.cs       #    - Handles gameplay interactions
├── Tests/                     # 🧪 Test instructions
│   └── MainMenuTests.cs      #    - Tests for main menu
├── Common/                    # 🔧 Shared utilities
└── run_tests.sh/.bat         # 🚀 Simple test runners
```

## 🔧 Customizing for Your Game

### Step 1: Update Game Object Names

1. **Open `Views/MainMenuView.cs`**
2. **Find this section:**
   ```csharp
   private readonly (By, string) PlayButton = (By.NAME, "PlayButtonName");
   ```
3. **Replace `"PlayButtonName"`** with your actual button name from Unity
4. **Repeat for other elements**

### Step 2: Add Your Own Tests

1. **Open `Tests/MainMenuTests.cs`**  
2. **Add a new test method:**
   ```csharp
   [Test]
   public void TestMyGameFeature()
   {
       // Your test steps here
       Reporter.Log("Testing my awesome feature");
       // Add assertions to verify behavior
   }
   ```

## 🐛 Troubleshooting (Common Beginner Issues)

### ❌ "Connection refused" or "Cannot connect"
**Problem:** AltTester can't connect to your game
**Solution:** 
1. Make sure your Unity game is running
2. Check that AltTester Desktop shows your game as connected
3. Verify the game has AltTester integration

### ❌ "Element not found" or "Object not found"  
**Problem:** Test can't find a button or game object
**Solution:**
1. Check the exact name of your game object in Unity
2. Update the locator in your View class
3. Make sure the object is visible when the test runs

### ❌ "Timeout" errors
**Problem:** Test is waiting too long for something to happen
**Solution:**
1. Increase timeout values in your test
2. Check if the expected action actually happens in the game
3. Add debug logs to see what's happening

### ❌ Tests fail randomly
**Problem:** Tests work sometimes but not always
**Solution:**
1. Add wait conditions before interactions
2. Check if your game loads at different speeds
3. Make tests more robust with proper waits

## 💡 Next Steps

Once you're comfortable with the basics:

1. **Add more Views** for different game screens
2. **Create more comprehensive tests** covering edge cases  
3. **Set up continuous integration** to run tests automatically
4. **Explore advanced features** like data-driven tests
5. **Integrate with reporting tools** for better test visibility

## 🛠️ Setup Instructions

### Prerequisites

1. **.NET 8.0 SDK** or later - [Download here](https://dotnet.microsoft.com/download)
2. **AltTester Unity SDK** integrated in your Unity project
3. **AltTester Desktop** - [Download here](https://altom.com/alttester/)
4. **Visual Studio** or **Visual Studio Code** (optional but recommended)
5. **Appium Server** (only if testing mobile platforms)
6. **Chrome browser** (only if testing WebGL)

### Installation

1. **Clone or download** this template project
2. **Navigate to the project folder** in terminal/command prompt
3. **Restore dependencies:**
   ```bash
   dotnet restore
   ```
4. **Build the project:**
   ```bash
   dotnet build
   ```

### Configuration

The project works with **default settings** out of the box for local testing. For advanced scenarios, you can customize:

#### Environment Variables:
- `ALT_TESTER_SERVER_URL` - AltTester server URL (default: "127.0.0.1")
- `ALT_TESTER_SERVER_PORT` - AltTester server port (default: 13000)
- `TEST_PLATFORM` - Target platform: Android, iOS, WebGL (default: "Android")
- `RUN_TESTS_WITH_APPIUM` - Enable Appium: true/false (default: "false")
- `RUN_TESTS_WITH_SELENIUM` - Enable Selenium: true/false (default: "false")

### Running Tests - Multiple Ways

#### Method 1: Simple Scripts (Beginner-Friendly)

**macOS/Linux:**
```bash
# Run all tests
./run_tests.sh

# Run specific test class  
./run_tests.sh MainMenuTests

# Run with custom output directory
./run_tests.sh MainMenuTests results
```

**Windows:**
```cmd
REM Run all tests
run_tests.bat

REM Run specific test class
run_tests.bat MainMenuTests

REM Run with custom output directory
run_tests.bat MainMenuTests results
```

#### Method 2: With Environment Variables

**macOS/Linux:**
```bash
# Change AltTester port
ALT_TESTER_SERVER_PORT=13001 ./run_tests.sh

# Run WebGL tests
TEST_PLATFORM=WebGL RUN_TESTS_WITH_SELENIUM=true ./run_tests.sh

# Run mobile tests with Appium
RUN_TESTS_WITH_APPIUM=true ./run_tests.sh
```

**Windows:**
```cmd
REM Change AltTester port
set ALT_TESTER_SERVER_PORT=13001 && run_tests.bat

REM Run WebGL tests
set TEST_PLATFORM=WebGL && set RUN_TESTS_WITH_SELENIUM=true && run_tests.bat

REM Run mobile tests with Appium
set RUN_TESTS_WITH_APPIUM=true && run_tests.bat
```

#### Method 3: Direct dotnet CLI (Advanced)

```bash
# Run all tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "MainMenuTests"

# Run tests with custom settings
dotnet test --logger "console;verbosity=detailed" --results-directory "./TestResults"
```

## 📝 Usage Examples

### Creating a New View Object

**Example 1: Custom Main Menu View**
```csharp
namespace AltTesterProject.Views;

public class MyCustomMenuView : BaseView
{
    // Define locators for your specific view
    private readonly (By, string) PlayButton = (By.NAME, "PlayButtonName");
    private readonly (By, string) SettingsButton = (By.NAME, "SettingsButtonName");

    public MyCustomMenuView(DriverContainer drivers) : base(drivers)
    {
    }

    // Define view actions
    public void ClickPlay()
    {
        var playButton = FindElement(PlayButton);
        playButton.Click();
    }

    public void OpenSettings()
    {
        var settingsButton = FindElement(SettingsButton);
        settingsButton.Click();
    }
}
```

**Example 2: Custom Gameplay View**
```csharp
namespace AltTesterProject.Views;

public class MyCustomGamePlayView : BaseView
{
    // Define locators for your specific gameplay elements
    private readonly (By, string) Player = (By.NAME, "PlayerCharacter");
    private readonly (By, string) HealthBar = (By.NAME, "HealthBar");

    public MyCustomGamePlayView(DriverContainer drivers) : base(drivers)
    {
    }

    // Define gameplay actions
    public void MovePlayer(float x, float y)
    {
        var player = FindElement(Player);
        // Custom movement logic here
    }

    public int GetPlayerHealth()
    {
        var healthBar = FindElement(HealthBar);
        // Extract health value logic here
        return 100;
    }
}
```

### Creating a New Test Class

```csharp
namespace AltTesterProject.Tests;

[TestFixture]
[AllureFeature("My Custom Feature")]
public class MyCustomTests : BaseTest
{
    private MyCustomMenuView? _menuView;
    private MyCustomGamePlayView? _gamePlayView;

    [SetUp]
    public void TestSetUp()
    {
        _menuView = new MyCustomMenuView(Drivers);
        _gamePlayView = new MyCustomGamePlayView(Drivers);
    }

    [Test]
    [AllureTest("Test game flow from menu to gameplay")]
    public void TestGameFlow()
    {
        Reporter.Log("Starting from main menu");
        _menuView.ClickPlay();

        Reporter.Log("Verifying gameplay loaded");
        Assert.That(_gamePlayView!.GetPlayerHealth(), Is.GreaterThan(0));
    }
}
```

## 🔧 Customization

### Updating Locators
1. Open `Views/MainMenuView.cs` and `Views/GamePlayView.cs`
2. Replace the example locators with actual game object names, IDs, or paths from your Unity project
3. Update the locator strategies (ByName, ByTag, ByComponent) to match your game's structure

### Adding New View Objects
Create new view classes that inherit from `BaseView` following the pattern in `MainMenuView.cs` and `GamePlayView.cs`

### Extending Tests
Add new test methods to the existing test classes or create new test classes following the same pattern

## 📊 Reporting

The project includes basic test reporting:

1. **JUnit XML reports** are generated automatically in the output directory
2. **Console output** shows detailed test execution with verbose logging
3. **Screenshots and logs** are captured automatically on test failures

For advanced reporting, you can integrate Allure or other reporting tools as needed.

## 🐛 Troubleshooting

### Common Issues

1. **Connection refused**: Ensure your Unity application is running with AltTester enabled
2. **Object not found**: Verify locators match actual game object names/paths
3. **Timeout errors**: Increase timeout values or check if objects are actually present
4. **Driver startup failures**: Check that required services (Appium, etc.) are running

### Debug Tips

- Use `Reporter.Log()` for detailed logging during test execution
- Screenshots are automatically taken on test failures and saved to the build output directory
- Unity logs are captured automatically and saved alongside screenshots
- Check Unity Console for AltTester connection logs
- The test runners show detailed output to help with debugging
- Test results are saved in JUnit XML format for analysis

## 🤝 Getting Started

1. **Update locators**: Edit `Views/MainMenuView.cs` and `Views/GamePlayView.cs` to match your game's UI elements
2. **Customize the tests**: Modify `Tests/MainMenuTests.cs` to test your specific game functionality
3. **Add more views**: Create additional view objects for other screens (settings, inventory, etc.)
4. **Extend tests**: Add more test methods or create new test classes as needed

This template provides a solid foundation with two common game views that you can build upon for any Unity game testing project.

## 📚 Additional Resources

- [AltTester Documentation](https://altom.com/alttester/)
- [NUnit Documentation](https://docs.nunit.org/)
- [Allure Documentation](https://docs.qameta.io/allure/)
- [Appium Documentation](https://appium.io/docs/)
- [Selenium Documentation](https://selenium-python.readthedocs.io/)

## 📄 License

This template is provided as-is for educational and development purposes. Modify and adapt as needed for your projects.
