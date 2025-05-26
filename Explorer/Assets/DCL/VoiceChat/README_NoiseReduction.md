# Voice Chat Noise Reduction & Audio Processing

This system provides comprehensive noise reduction and audio processing capabilities for the voice chat microphone input.

## Features

### 1. **Noise Gate**
- **Purpose**: Eliminates background noise when not speaking
- **How it works**: Mutes audio below a specified threshold with intelligent timing
- **Settings**:
  - `EnableNoiseGate`: Enable/disable the noise gate
  - `NoiseGateThreshold`: Volume threshold (0-1) below which audio is muted
  - `NoiseGateHoldTime`: Time (0.1-2s) to keep gate open after speech ends (prevents cutting off word endings)
  - `NoiseGateAttackTime`: How quickly gate opens when speech detected (0.01-0.5s)
  - `NoiseGateReleaseTime`: How quickly gate closes after hold time (0.01-1s)
- **Recommended**: 
  - `NoiseGateThreshold = 0.01` for most environments
  - `NoiseGateHoldTime = 0.3s` to prevent cutting off word endings
  - `NoiseGateAttackTime = 0.05s` for quick response
  - `NoiseGateReleaseTime = 0.1s` for smooth transitions

### 2. **High-Pass Filter**
- **Purpose**: Removes low-frequency noise (air conditioning, traffic rumble, etc.)
- **How it works**: Filters out frequencies below the cutoff point
- **Settings**:
  - `EnableHighPassFilter`: Enable/disable the filter
  - `HighPassCutoffFreq`: Cutoff frequency in Hz (50-500Hz)
- **Recommended**: `HighPassCutoffFreq = 80Hz` for voice chat

### 3. **Automatic Gain Control (AGC)**
- **Purpose**: Normalizes volume levels for consistent audio
- **How it works**: Automatically adjusts gain to maintain target volume
- **Settings**:
  - `EnableAutoGainControl`: Enable/disable AGC
  - `AGCTargetLevel`: Target volume level (0.1-1.0)
  - `AGCResponseSpeed`: How quickly AGC responds (0.1-5.0)
- **Recommended**: `AGCTargetLevel = 0.7`, `AGCResponseSpeed = 1.0`

### 4. **Enhanced Noise Reduction**
- **Purpose**: Intelligently reduces background noise while preserving speech quality
- **How it works**: 
  - Adaptive learning of both noise and speech patterns
  - Multi-stage frequency-aware noise reduction
  - Speech-aware processing to prevent voice distortion
  - Continuous adaptation to changing noise environments
- **Settings**:
  - `EnableNoiseReduction`: Enable/disable noise reduction
  - `NoiseReductionStrength`: Reduction strength (0-1)
- **Recommended**: `NoiseReductionStrength = 0.8` for effective fan noise reduction (now artifact-resistant)
- **Key Improvements**:
  - **Dual-stage processing**: Pre-AGC aggressive reduction + Post-AGC gentle cleanup
  - Learns separate profiles for noise and speech
  - Frequency-domain analysis for better noise characterization
  - Adaptive thresholds that adjust to environment
  - AGC-aware noise reduction that accounts for amplification levels
  - Speech-aware processing prevents voice distortion

## Implementation

### Components

1. **VoiceChatAudioProcessor**: Core audio processing engine
2. **VoiceChatMicrophoneAudioFilter**: Unity AudioFilter component for real-time processing
3. **VoiceChatMicrophoneHandler**: Integrates processing with microphone handling

### Configuration

All settings are configured in the `VoiceChatSettingsAsset`:

```csharp
[Header("Noise Reduction & Audio Processing")]
public bool EnableNoiseGate = true;
public float NoiseGateThreshold = 0.01f;
public float NoiseGateHoldTime = 0.3f;
public float NoiseGateAttackTime = 0.05f;
public float NoiseGateReleaseTime = 0.1f;
public bool EnableHighPassFilter = true;
public float HighPassCutoffFreq = 80f;
public bool EnableAutoGainControl = true;
public float AGCTargetLevel = 0.7f;
public float AGCResponseSpeed = 1f;
public bool EnableNoiseReduction = true;
public float NoiseReductionStrength = 0.3f;
```

### Performance Considerations

- **Real-time Processing**: All processing happens in Unity's audio thread
- **Memory Efficient**: Reuses buffers to minimize allocations
- **CPU Optimized**: Simple algorithms designed for real-time performance
- **Adaptive**: Noise learning happens automatically during quiet periods

## Usage

### Basic Setup
1. The system is automatically initialized when the microphone handler starts
2. Default settings work well for most environments
3. No additional setup required

### Advanced Configuration
1. Adjust settings in the `VoiceChatSettingsAsset`
2. Test different values based on your environment
3. Monitor performance using the provided properties:
   - `IsNoiseGateOpen`: Check if noise gate is currently open
   - `CurrentGain`: Monitor AGC gain levels

### Troubleshooting

**Audio sounds muffled:**
- Reduce `NoiseReductionStrength`
- Lower `HighPassCutoffFreq`

**Background noise still audible (especially fan noise):**
- Increase `NoiseReductionStrength` to 0.7-0.8
- Increase `NoiseGateThreshold` to 0.015-0.02
- Ensure `EnableNoiseGate` is true
- Lower `AGCResponseSpeed` to 0.5-0.8 to prevent noise amplification
- Increase `HighPassCutoffFreq` to 120-150Hz for fan noise

**Volume inconsistent:**
- Enable `EnableAutoGainControl`
- Adjust `AGCTargetLevel`
- Increase `AGCResponseSpeed` for faster adjustment

**Audio cutting in/out:**
- Lower `NoiseGateThreshold`
- Increase `NoiseGateHoldTime` (try 0.5-1.0 seconds)
- Reduce `NoiseReductionStrength`
- Increase `NoiseGateAttackTime` for slower gate opening

**Words getting cut off at the end:**
- Increase `NoiseGateHoldTime` (recommended: 0.3-0.5 seconds)
- Increase `NoiseGateReleaseTime` for smoother fade-out

## Technical Details

### Processing Order
1. High-pass filter (removes low frequencies)
2. Pre-AGC noise reduction (aggressive reduction of obvious noise)
3. Noise gate (mutes quiet audio)
4. Automatic gain control (normalizes volume)
5. Post-AGC noise reduction (gentle artifact-resistant cleanup)

### Adaptive Learning System
- **Noise Learning**: Continuously learns noise patterns during silence periods (>0.5s quiet)
- **Speech Learning**: Learns speech characteristics during active speech periods (>0.2s talking)
- **Frequency Analysis**: Analyzes 32 frequency bins for better noise characterization
- **Adaptive Thresholds**: Automatically adjusts detection thresholds based on environment
- **Continuous Adaptation**: Profiles update throughout the session, not just at startup
- **Profile Reset**: All profiles reset when microphone changes

### Performance Impact
- Minimal CPU usage (~1-2% on modern systems)
- No significant memory allocations during runtime
- Optimized for real-time audio processing 