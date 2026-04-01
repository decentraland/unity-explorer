# PR 1: Core Audio Tracks — Детальный план

## Результат

При подключении к Island Room игроки автоматически публикуют аудио и слышат друг друга с 3D spatial audio. Координация с Private/Community calls (suppress/resume).

---

## Принятые решения

| Решение | Выбор | Обоснование |
|---|---|---|
| Publish mode | Lazy publish, state model стартует в Speaking | Speaking при старте = auto-publish. Lazy publish сохранён |
| Mute service | Не переносим в PR 1 | Manager без mute кода, MuteService появится в PR 2 |
| Plugin DI | Минимальные изменения | Не меняем сигнатуру конструктора VoiceChatPlugin |
| ConfigHolder | Только `Config` поле | Без MouthTextureArray, без SpeakingParticipants |
| Config fields | Только proximity audio | Без LipSync полей |
| ActiveSpeakers | Нет в PR 1 | Подписка на ActiveSpeakers.Updated — в PR 2 с nametags |
| Identity wait | Не ждём | Proximity инициализируется сразу в InitializeAsync |
| Audio Mixer | Копируем .mixer из source | GUID'ы совпадают т.к. оба repo из одного dev |
| Suppress/Resume | Оставляем | Координация с calls, ResumeProximity упрощён (unmute all) |
| Initial state | `new StateModel(Speaking)` в Plugin | StateModel 1:1 с source (default=Hearing), Plugin передаёт Speaking |
| LiveKit SDK | Ветка `feat/mono-spatial-audio` | Нужна для SetSpatialAngles, ILDMode, ITD/HRTF |
| .meta файлы | Только для ассетов | .cs — Unity генерирует, .mixer/.asset — копируем .meta |

---

## Файлы

### Создаём
```
Proximity/UI/ProximityVoiceChatState.cs
Proximity/ProximityVoiceChatStateModel.cs
Proximity/ProximityAudioSourceComponent.cs
Proximity/ProximityAudioSettings.cs              (ProximityConfigHolder)
Proximity/ProximityPanCalculator.cs
Proximity/ProximityVoiceChatManager.cs            (без mute кода)
Proximity/ProximityAudioDebugWidget.cs
Proximity/Systems/ProximityAudioPositionSystem.cs
```

### Модифицируем
```
VoiceChat/VoiceChatConfiguration.cs               +proximity audio fields, +Apply methods
PluginSystem/Global/VoiceChatPlugin.cs             +proximity wiring
AvatarRendering/.../FindAvatarUtils.cs             изменение из source
Packages/manifest.json                             LiveKit SDK → feat/mono-spatial-audio
Packages/packages-lock.json                        hash update
```

### Копируем (binary/asset + .meta)
```
Audio/Prefabs/GeneralAudioMixer.mixer + .meta
VoiceChat/VoiceChatConfiguration.asset + .meta
```

---

## 5 микро-итераций

### Микро 1: LiveKit SDK + Enum + StateModel

**Файлы:**
- `Packages/manifest.json` — LiveKit SDK → `feat/mono-spatial-audio`
- `Packages/packages-lock.json` — обновить hash
- `Proximity/UI/ProximityVoiceChatState.cs` — enum
- `Proximity/ProximityVoiceChatStateModel.cs` — state machine

**Содержание:**
- `ProximityVoiceChatState`: Disconnected, Hearing, Speaking, Blocked
- `ProximityVoiceChatStateModel`: ReactiveProperty-based state machine
  - Enable/Disable, StartSpeaking/StopSpeaking
  - Suppress/Resume (запоминает pre-blocked state)
  - Конструктор `(initialState = Hearing)` — 1:1 с source

---

### Микро 2: Configuration + ECS Component + ConfigHolder

**Файлы:**
- `VoiceChat/VoiceChatConfiguration.cs` — модификация
- `Proximity/ProximityAudioSourceComponent.cs` — новый
- `Proximity/ProximityAudioSettings.cs` — новый (ProximityConfigHolder)
- `Audio/Prefabs/GeneralAudioMixer.mixer` + `.meta` — копия из source
- `VoiceChat/VoiceChatConfiguration.asset` + `.meta` — копия из source

**Содержание VoiceChatConfiguration (добавляем):**
- `ProximityChatAudioMixerGroup` (AudioMixerGroup)
- Spatial: SpatialBlend (1.0), DopplerLevel (0), MinDistance (2), MaxDistance (16), Spread (0)
- Rolloff: RolloffMode (Custom), CustomRolloffCurve (5 keyframes)
- Spatialization: ProximityILDMode (HeadShadow), EnableITD (false), EnableHRTF (false)
- `ApplyProximitySettingsTo(AudioSource)` — применяет все spatial настройки
- `ApplySpatializationSettingsTo(LivekitAudioSource)` — применяет ILD/ITD/HRTF
- **Без** LipSync полей

**ProximityConfigHolder:** только `public VoiceChatConfiguration? Config;`

**ProximityAudioSourceComponent:** struct { Transform AudioSourceTransform; AudioSource AudioSource; }

---

### Микро 3: PanCalculator

**Файлы:**
- `Proximity/ProximityPanCalculator.cs` — новый

**Содержание:**
- MonoBehaviour `[RequireComponent(typeof(LivekitAudioSource))]`
- `Update()`: вычисляет azimuth (Atan2 x/z) и elevation (Atan2 y/horizontalDist)
- Передаёт в `livekitAudioSource.SetSpatialAngles(azimuth, elevation)`
- Ленивый поиск AudioListener через `FindAnyObjectByType`

---

### Микро 4: ProximityAudioPositionSystem

**Файлы:**
- `Proximity/Systems/ProximityAudioPositionSystem.cs` — новый
- `AvatarRendering/.../FindAvatarUtils.cs` — модификация (если нужна)

**Содержание:**
- `[UpdateInGroup(PresentationSystemGroup)]`, `[UpdateAfter(MultiplayerProfilesSystem)]`
- `partial class`, source-generated queries
- `AssignPendingSources()` — итерирует shared dict, маппит walletId→Entity через entityParticipantTable
- `SyncPositions()` [Query] — position = camera + (remoteHead - localHead), AvatarBase.HeadAnchorPoint с fallback 1.75m
- `ApplySettings()` [Query] — configHolder.Config.ApplyProximitySettingsTo(audioSource)
- `ProcessCleanUp()` — Remove component при null Transform

---

### Микро 5: Manager + Plugin wiring + Debug Widget

**Файлы:**
- `Proximity/ProximityVoiceChatManager.cs` — новый
- `Proximity/ProximityAudioDebugWidget.cs` — новый
- `PluginSystem/Global/VoiceChatPlugin.cs` — модификация

**ProximityVoiceChatManager (отличия от source):**
- Конструктор: `(IRoom, VoiceChatConfiguration, ConcurrentDictionary<string, AudioSource>, IReadonlyReactiveProperty<VoiceChatStatus>, ProximityVoiceChatStateModel)`
- **Убрано:** ProximityMuteService, OnMuteStateChanged, IsMuted проверки
- **Оставлено:** suppress/resume (SuppressProximity: mute all, ResumeProximity: unmute all)
- **Упрощено:** ResumeProximity — `audioSource.mute = false` без IsMuted проверки
- AddRemoteSource: без `proximityMuteService.IsMuted()` проверки
- Остальное 1:1: publish/subscribe, retry, connection events, state changes, microphone switch

**VoiceChatPlugin изменения:**
- Новые поля: `proximityAudioSources`, `proximityConfigHolder`, `proximityVoiceChatManager`, `proximityStateModel`
- `InjectToWorld()`: `ProximityAudioDebugWidget.Setup()` + `ProximityAudioPositionSystem.InjectToWorld()`
- `InitializeAsync()` в конце: создаём configHolder → stateModel(Speaking) → manager
- `Dispose()`: cleanup
- **Не меняем** сигнатуру конструктора

**ProximityAudioDebugWidget:** 1:1 с source — статический Setup, runtime слайдеры для spatial audio

---

## Верификация

1. Два клиента на одном острове
2. Логи: `Initialized` → `Activated — publishing and listening with 3D spatial audio`
3. Слышно удалённых игроков, звук позиционирован в 3D
4. При Private/Community call → `Suppressed`, proximity замьючен
5. После звонка → `Resumed`, proximity восстановлен
6. Debug виджет: слайдеры работают (spatial blend, distance, rolloff)
7. Дисконнект от острова → `Deactivated`, cleanup
