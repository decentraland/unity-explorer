# Settings Panel

## Overview

The Settings Panel in the Decentraland Unity Explorer is a modular, configurable system that allows users to adjust various aspects of their experience. The system is designed with flexibility in mind, supporting different types of controls and feature flag-based visibility.

## Architecture

The settings system follows the following structure:

### Core Components

- **SettingsController**: Main orchestrator that manages the settings panel lifecycle and navigation
- **SettingsMenuConfiguration**: ScriptableObject that defines the structure and content of the settings panel
- **SettingsFeatureController**: Base class for individual setting controllers that handle specific functionality
- **SettingsModuleView**: UI components that render the actual controls

### Module Types

The system supports three main types of controls:

1. **Toggle Controls** (`ToggleModuleBinding`)
   - Simple on/off switches
   - Examples: V-Sync, Chat Sounds, Hide Blocked Users

2. **Slider Controls** (`SliderModuleBinding`)
   - Range-based controls with different display types
   - Types: Numeric, Percentage, Time, Custom
   - Examples: Volume controls, Sensitivity settings, Distance settings

3. **Dropdown Controls** (`DropdownModuleBinding`)
   - Selection from predefined options
   - Support for single and multi-select
   - Examples: Graphics Quality, Resolution, Window Mode

Additional types of controls will need to be created by following the same structure of the existing ones.

## Settings Sections

The settings panel is organized into 4 main sections:

### 1. Graphics Section
- Visual quality settings
- Resolution and display options
- Performance graphics options
- V-Sync and frame rate controls

### 2. Sound Section
- Master volume control
- Individual volume sliders for:
  - World sounds
  - Music
  - UI sounds
  - Avatar sounds
  - Voice chat
- Audio device selection

### 3. Controls Section
- Mouse sensitivity settings
- Camera controls
- Input device configuration
- Control scheme options

### 4. Chat Section
- Chat bubble visibility
- Chat privacy settings
- Audio modes for different chat types
- Blocked users management

## Adding New Settings

### Step 1: Create the Module View

Create a new view class inheriting from the appropriate base (slider in this example):

```csharp
public class MyCustomSliderView : SettingsSliderModuleView
{
    ...
}
```

### Step 2: Create the Controller

Create a controller that inherits from `SettingsFeatureController`:

```csharp
public class MyCustomSettingController : SettingsFeatureController
{
    private readonly MyCustomSliderView view;

    public MyCustomSettingController(MyCustomSliderView view)
    {
        this.view = view;

        view.SliderView.Slider.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnValueChanged(float newValue)
    {

    }

    public override void Dispose()
    {
        view.SliderView.Slider.onValueChanged.RemoveAllListeners();
    }
}
```

### Step 3: Add to Module Binding

Add your new feature to the appropriate module binding enum and switch statement:

```csharp
public enum SliderFeatures
{
    // ... existing features
    MY_CUSTOM_SLIDER_FEATURE,
}

SettingsFeatureController controller = Feature switch
{
    // ... existing cases
    SliderFeatures.MY_CUSTOM_SLIDER_FEATURE => new MyCustomSettingController(viewInstance, dataStore),
    _ => throw new ArgumentOutOfRangeException(),
};
```

> **Warning:** Be aware to add your feature at the end of `SliderFeatures`, otherwise changing order will break it in the Scriptable Object that uses it.

### Step 4: Configure in Settings Menu

1. Open the `SettingsMenuConfiguration` asset in Unity
2. Navigate to the appropriate section (Graphics, Sound, Controls, or Chat)
3. Add a new `SettingsGroup` or use an existing one
4. Add your module binding to the group's modules list
5. Configure the module with appropriate title, description, and default values

## Feature Flags

The settings system supports feature flag-based visibility to enable/disable entire sections or individual settings groups.

### Using Feature Flags

1. **Define the Feature Flag**: Add your feature flag to the `FeatureFlag` enum in `FeatureFlagsStrings.cs`

```csharp
public enum FeatureFlag
{
    // ... existing flags
    MyNewFeature,
}
```

2. **Apply to Settings Group**: In the `SettingsMenuConfiguration`, set the `FeatureFlagName` property on your `SettingsGroup`

3. **Runtime Behavior**: The system automatically checks if the feature flag is enabled using:

```csharp
FeatureFlagsConfiguration.Instance.IsEnabled(group.FeatureFlagName.GetStringValue())
```
