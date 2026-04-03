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
| ConfigHolder | Удаляем в фазе 6.1 | Заменяем на direct injection + SetConfiguration() |
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

---

## Микро 6: Архитектурное выравнивание с Private/Community VoiceChat

По результатам архитектурного ревью — ProximityVoiceChatManager (572 строк) является монолитом,
совмещающим публикацию микрофона, подписку на удалённые треки, создание spatial audio источников,
state-координацию и lifecycle. В существующей архитектуре (Private/Community) эти ответственности
разделены между `MicrophoneTrackPublisher`, `RemoteTrackListener` + `PlaybackSourcesHub`,
`VoiceChatRoomManager`, `VoiceChatMicrophoneHandler`.

Четыре фазы рефакторинга, в порядке от простого к сложному:

---

### Фаза 6.1: Удаление ProximityConfigHolder — DONE

**Проблема:** `ProximityConfigHolder` — обёртка с одним полем (`public VoiceChatConfiguration? Config`),
существует из-за того что `InjectToWorld` вызывается ДО `InitializeAsync` (где загружается конфиг).
Manager уже получает `VoiceChatConfiguration` напрямую — только ECS-система и debug widget зависят от holder.

**Ключевая находка:** `InjectToWorld` **возвращает** ссылку на систему — подтверждено в
`PropagateAvatarLocomotionOverridesSystem`, `AvatarAttachHandlerSystem`, `TrackPlayerPositionSystem` и др.

**Файлы:**

| Файл | Действие |
|---|---|
| `Proximity/ProximityConfigHolder.cs` + `.meta` | Удалить |
| `Proximity/Systems/ProximityAudioPositionSystem.cs` | Модификация |
| `Proximity/ProximityAudioDebugWidget.cs` | Модификация |
| `PluginSystem/Global/VoiceChatPlugin.cs` | Модификация |

**Шаги:**

**6.1.1** `ProximityAudioPositionSystem.cs`:
- Конструктор: `ProximityConfigHolder configHolder` → `VoiceChatConfiguration? configuration = null`
- Поле: `private VoiceChatConfiguration? configuration;` (мутабельное, не readonly)
- Добавить сеттер: `internal void SetConfiguration(VoiceChatConfiguration config) => configuration = config;`
- `ApplySettings` (строка 115-118): `configHolder.Config!.ApplyProximitySettingsTo(...)` → `configuration?.ApplyProximitySettingsTo(...)`
- **Безопасность:** между InjectToWorld и InitializeAsync нет proximity audio sources (Manager создаётся в InitializeAsync), ApplySettings просто no-op пока config == null. Закомментированный guard на строке 58 (`// if (configHolder.Config == null) return;`) подтверждает что автор это предусмотрел.

**6.1.2** `ProximityAudioDebugWidget.cs`:
- Сигнатура: `Setup(IDebugContainerBuilder, ProximityConfigHolder)` → `Setup(IDebugContainerBuilder, VoiceChatConfiguration)`
- Убрать все `if (configHolder.Config != null)` guard'ы — конфиг гарантированно не-null при вызове
- Вызов **переносится** из `InjectToWorld` в `InitializeAsync` (после загрузки конфига)

**6.1.3** `VoiceChatPlugin.cs`:
- Удалить поле `proximityConfigHolder` (строка 48)
- Добавить поле: `private ProximityAudioPositionSystem? proximityAudioPositionSystem;`
- `InjectToWorld` (строка 102-106):
  - Убрать `ProximityAudioDebugWidget.Setup(...)` — перенос в InitializeAsync
  - Сохранить ссылку: `proximityAudioPositionSystem = ProximityAudioPositionSystem.InjectToWorld(ref builder, entityParticipantTable, proximityAudioSources);`
- `InitializeAsync` (после строки 118):
  - `proximityAudioPositionSystem!.SetConfiguration(voiceChatConfiguration);`
  - `ProximityAudioDebugWidget.Setup(debugContainer, voiceChatConfiguration);`
  - Удалить строку 154 (`proximityConfigHolder.Config = voiceChatConfiguration;`)

**6.1.4** Удалить `ProximityConfigHolder.cs` + `.meta`

---

### Фаза 6.2: Разделение Manager на 2 класса — DONE

**Проблема:** 572-строчный монолит с 4 группами ответственности и cross-cutting зависимостями.

**Паттерн:** Повторяем `VoiceChatRoomManager` (оркестратор) + `RemoteTrackListener` (remote-треки).

**Файлы:**

| Файл | Действие |
|---|---|
| `Proximity/ProximityRemoteTrackListener.cs` + `.meta` | Создать (~180 строк) |
| `Proximity/ProximityVoiceChatManager.cs` | Модификация (572 → ~250 строк) |

#### Новый класс: `ProximityRemoteTrackListener : IDisposable`

**Ответственность:** Весь lifecycle удалённых audio sources — подписка, создание spatial GameObject'ов,
регистрация в shared dictionary для ECS bridge, mute/unmute при suppress/resume, loopback.

**Конструктор:**
```csharp
internal ProximityRemoteTrackListener(
    IRoom islandRoom,
    VoiceChatConfiguration configuration,
    ConcurrentDictionary<string, AudioSource> activeAudioSources)
```

**Перенос методов из Manager:**

| Из Manager (строки) | В ProximityRemoteTrackListener | Видимость |
|---|---|---|
| `SubscribeToExistingRemoteTracks` (238-251) | `StartListening()` | internal |
| `OnTrackSubscribed` (253-277) | `HandleTrackSubscribed(TrackPublication, Participant)` | internal |
| `OnTrackUnsubscribed` (279-299) | `HandleTrackUnsubscribed(TrackPublication, Participant)` | internal |
| `OnLocalTrackPublished` (301-331) | `HandleLocalTrackPublished(TrackPublication, Participant)` | internal |
| `OnLocalTrackUnpublished` (333-356) | `HandleLocalTrackUnpublished(TrackPublication, Participant)` | internal |
| `AddRemoteSource` (358-381) | `AddRemoteSource` | private |
| `RemoveRemoteSource` (383-391) | `RemoveRemoteSource` | private |
| `CreateSource` (393-410) | `CreateSource` | private |
| `DestroySource` (412-419) | `DestroySource` | private static |
| Часть `SuppressProximity` (486-490) | `MuteAll()` | internal |
| `ResumeProximity` (495-509) | `UnmuteAll()` | internal |
| Часть `Deactivate` (541-545) | `StopListening()` | internal |

**Перенос полей:**
- `remoteSources` (ConcurrentDictionary<StreamKey, LivekitAudioSource>)
- `activeAudioSources` (ConcurrentDictionary<string, AudioSource>) — ссылка
- `fallbackParent` (Transform) — создаётся в конструкторе listener'а
- `loopbackSource` (LivekitAudioSource?)
- `suppressed` (bool) — привязан к remote sources

#### Рефакторинг ProximityVoiceChatManager (оркестратор)

**Остаётся в Manager:**
- Конструктор: создаёт `ProximityRemoteTrackListener`, подписывается на room events, делегирует
- `Dispose`: dispose listener, отписка от событий
- `OnConnectionUpdated`: без изменений логики
- `ActivateWithRetryAsync`: вызывает `PublishLocalTrackAsync` + `remoteListener.StartListening()`
- `PublishLocalTrackAsync`: без изменений (только local track)
- `UnpublishLocalTrack`: без изменений
- `OnCallStatusChanged`: без изменений
- `OnProximityStateChanged`: `SuppressProximity` → `rtcAudioSource?.Stop()` + `remoteListener.MuteAll()`
- `Deactivate`: `UnpublishLocalTrack()` + `remoteListener.StopListening()`
- `OnMicrophoneChanged` / `SwitchMicrophoneInternal`: без изменений

**Room event wiring (паттерн из VoiceChatRoomManager):**
```csharp
// Manager подписывается на room events и делегирует в listener:
islandRoom.TrackSubscribed += (track, pub, p) => remoteListener.HandleTrackSubscribed(pub, p);
islandRoom.TrackUnsubscribed += (track, pub, p) => remoteListener.HandleTrackUnsubscribed(pub, p);
islandRoom.LocalTrackPublished += (pub, p) => remoteListener.HandleLocalTrackPublished(pub, p);
islandRoom.LocalTrackUnpublished += (pub, p) => remoteListener.HandleLocalTrackUnpublished(pub, p);
```

**Cross-cutting зависимости (анализ):**

| Поле | Группы | Решение |
|---|---|---|
| `islandRoom` | Все | Оба класса получают в конструкторе |
| `configuration` | Local + Remote | Оба получают в конструкторе |
| `rtcAudioSource` | Local + State | Остаётся в Manager |
| `published` | Local + State | Остаётся в Manager |
| `suppressed` | Remote + State | Перемещается в Listener, Manager вызывает `MuteAll()`/`UnmuteAll()` |
| `remoteSources` | Remote + State + Lifecycle | Перемещается в Listener, Manager вызывает `StopListening()` |

**Потокобезопасность:** `suppressed` читается/пишется только с main thread (все обработчики делают `await UniTask.SwitchToMainThread()` первым делом) — lock не нужен.

---

### Фаза 6.2.5: Извлечение ProximityMicrophoneTrackPublisher — DONE

**Внеплановое.** По аналогии с core `MicrophoneTrackPublisher` — извлечена вся mic-логика
из Manager'а в отдельный `ProximityMicrophoneTrackPublisher`:

| Файл | Действие |
|---|---|
| `Proximity/ProximityMicrophoneTrackPublisher.cs` | Создать (~130 строк) |
| `Proximity/ProximityVoiceChatManager.cs` | Модификация (250 → ~180 строк) |

**API:** `PublishAsync(ct)`, `Unpublish()`, `StartMicrophone()`, `StopMicrophone()`, `IsPublished`.
Также полностью перенесены: `VoiceChatSettings.MicrophoneChanged` подписка, `SwitchMicrophone`, `PUBLISH_OPTIONS` static.

Manager стал чистым оркестратором без прямого владения mic-ресурсами.

**Дополнительно (полировка 6.2):** listener отрефакторен по образцу core `RemoteTrackListener` —
`TryAddRemoteSource(kind, key)` DRY-метод, `isLocalLoopback` параметр вместо отдельных HandleLocalTrack* методов,
`TAG` паттерн, try/catch в `StartListening`.

---

### Фаза 6.3: Выравнивание микрофона

Три подшага. Выполняются на уже разделённом Manager'е (~180 строк).

#### 6.3a. Owned/Weak паттерн для MicrophoneRtcAudioSource

**Проблема:** Manager хранит `MicrophoneRtcAudioSource?` как raw nullable (строка 52).
`MicrophoneTrackPublisher` (Core) оборачивает в `Owned<T>` и передаёт `Weak<T>` — паттерн lifetime safety.

**Решение:** Извлечь `MicrophoneTrack` struct из `MicrophoneTrackPublisher` (строки 171-190) в shared файл.
Обоснование: struct идентичен в обоих consumer'ах, извлечение предотвращает drift.

**Файлы:**

| Файл | Действие |
|---|---|
| `VoiceChat/Core/MicrophoneTrack.cs` + `.meta` | Создать (извлечь из MicrophoneTrackPublisher) |
| `VoiceChat/Core/MicrophoneTrackPublisher.cs` | Модификация (использовать shared struct) |
| `Proximity/ProximityVoiceChatManager.cs` | Модификация |

**Shared struct:**
```csharp
// Assets/DCL/VoiceChat/Core/MicrophoneTrack.cs
namespace DCL.VoiceChat
{
    internal readonly struct MicrophoneTrack : IDisposable
    {
        private readonly Owned<MicrophoneRtcAudioSource> source;
        public ITrack Track { get; }
        public Weak<MicrophoneRtcAudioSource> Source => source.Downgrade();

        public MicrophoneTrack(ITrack track, Owned<MicrophoneRtcAudioSource> source)
        {
            Track = track;
            this.source = source;
        }

        public void Dispose()
        {
            source.Dispose(out MicrophoneRtcAudioSource? inner);
            inner?.Dispose();
        }
    }
}
```

**Замены в ProximityVoiceChatManager:**
- Поля `rtcAudioSource`, `localTrack`, `published` → одно поле `MicrophoneTrack? microphoneTrack`
- `PublishLocalTrackAsync`: `microphoneTrack = new MicrophoneTrack(track, new Owned<MicrophoneRtcAudioSource>(src))`
- `UnpublishLocalTrack`: unpublish track, затем `microphoneTrack?.Dispose(); microphoneTrack = null;`
- `published` проверки → `microphoneTrack.HasValue`
- `rtcAudioSource?.Start()/Stop()` → доступ через `microphoneTrack?.Source.Resource` с `.Has` проверкой:
  ```csharp
  // Было:
  rtcAudioSource?.Start();
  // Стало:
  if (microphoneTrack is { } mt) { var res = mt.Source.Resource; if (res.Has) res.Value.Start(); }
  ```
- `SwitchMicrophoneInternal` — аналогично через Weak

**Замены в MicrophoneTrackPublisher:**
- Убрать вложенный `MicrophoneTrack` struct (строки 171-190)
- Использовать `DCL.VoiceChat.MicrophoneTrack` из shared файла

#### 6.3b. Вынести общий VoiceChatTrackPublishHelper

**Проблема:** ~85% дублирования между `MicrophoneTrackPublisher.PublishAsync` (строки 76-113)
и `ProximityVoiceChatManager.PublishLocalTrackAsync` (строки 187-236):
- Windows volume hack (`audioMixer.SetFloat("Microphone_Volume", 13)`)
- macOS permission guard (`VoiceChatPermissions.GuardAsync`)
- Microphone selection (`VoiceChatSettings.ReachableSelection()`)
- `MicrophoneRtcAudioSource.New(...)` с идентичными параметрами
- `TrackPublishOptions` (124000 bitrate, SourceMicrophone)

**Файлы:**

| Файл | Действие |
|---|---|
| `VoiceChat/Core/VoiceChatTrackPublishHelper.cs` + `.meta` | Создать |
| `VoiceChat/Core/MicrophoneTrackPublisher.cs` | Модификация (использовать helper) |
| `Proximity/ProximityVoiceChatManager.cs` | Модификация (использовать helper) |

**Static helper:**
```csharp
namespace DCL.VoiceChat
{
    internal static class VoiceChatTrackPublishHelper
    {
        internal static async UniTask<MicrophoneRtcAudioSource> CreateMicrophoneSourceAsync(
            VoiceChatConfiguration configuration, CancellationToken ct)
        {
            // 1. Windows volume hack
            // 2. macOS permissions
            // 3. VoiceChatSettings.ReachableSelection()
            // 4. MicrophoneRtcAudioSource.New(selection, mixer, playbackToSpeakers)
            // Throws InvalidOperationException on failure
        }

        internal static TrackPublishOptions DefaultAudioPublishOptions() => new ()
        {
            AudioEncoding = new AudioEncoding { MaxBitrate = 124000 },
            Source = TrackSource.SourceMicrophone,
        };
    }
}
```

**Каждый publisher после рефакторинга:**
- `MicrophoneTrackPublisher`: семафор, `handler.Assign(weak)`, track name = participant name, notification on failure
- `ProximityVoiceChatManager`: `MicrophoneTrack` creation, track name = `proximity_{name}`, auto-start

#### 6.3c. DisconnectReason проверка при дисконнекте — DONE

**Файл:** `Proximity/ProximityVoiceChatManager.cs`

**Что:** В `OnConnectionUpdated`, case `Disconnected` (строки 139-142) — добавить проверку
`VoiceChatDisconnectReasonHelper.IsValidDisconnectReason(reason)` (паттерн из `VoiceChatRoomManager:336`).

**Текущий код:**
```csharp
case ConnectionUpdate.Disconnected:
    activationCts.SafeCancelAndDispose();
    Deactivate();
    break;
```

**Новый код:**
```csharp
case ConnectionUpdate.Disconnected:
    activationCts.SafeCancelAndDispose();
    Deactivate();

    if (VoiceChatDisconnectReasonHelper.IsValidDisconnectReason(disconnectReason))
        ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT,
            $"Valid disconnect ({disconnectReason}) — no reactivation needed");
    else
        ReportHub.Log(ReportCategory.PROXIMITY_VOICE_CHAT,
            $"Unexpected disconnect ({disconnectReason}) — will reactivate on next Connected event");
    break;
```

> Примечание: Для proximity это прежде всего **логгирование** — Island Room переподключается автоматически.
> Готовит к будущей логике (не реактивировать при `ClientInitiated`).

#### 6.3d. VoiceChatMicrophoneHandler — НЕ интегрируем

**Решение:** Оставить proximity mic management отдельным.
**Обоснование:**
- Proximity mic всегда включён в SPEAKING (auto-publish), нет toggle/PTT
- `VoiceChatMicrophoneHandler` жёстко связан с `ICommunityCallOrchestrator` для mute-нотификаций (строки 28, 188-199)
- Разная семантика: proximity driven by state machine, Private/Community driven by user input
- Совмещение нарушит SRP

---

### Фаза 6.4: Адаптация PlaybackSourcesHub (исследовательская)

**Статус:** Решение принимаем после завершения фаз 6.1-6.3.

**Идея:** `ProximityRemoteTrackListener` (созданный в 6.2) может переиспользовать `PlaybackSourcesHub`
вместо собственного `ConcurrentDictionary<StreamKey, LivekitAudioSource>`.

**Ключевая разница в source creation:**
- Существующий `PlaybackSourcesHub`: `LivekitAudioSource.New(true)` + ChatAudioMixerGroup
- Proximity: `LivekitAudioSource.New(explicitName: true, spatial: true)` + ProximityChatAudioMixerGroup + `ApplyProximitySettingsTo()` + `ApplySpatializationSettingsTo()` + `AddComponent<ProximityPanCalculator>()`

**Предлагаемый подход — source factory delegate:**
```csharp
// PlaybackSourcesHub.cs — добавить optional factory
internal PlaybackSourcesHub(
    AudioMixerGroup audioMixerGroup,
    Func<StreamKey, Weak<AudioStream>, LivekitAudioSource>? sourceFactory = null)
```
- Default factory (существующее поведение): `LivekitAudioSource.New(true)` + mixer group
- Proximity factory: создаёт с `spatial: true`, применяет spatial settings, добавляет `ProximityPanCalculator`
- Существующий `RemoteTrackListener` не затрагивается (использует default)

**Дополнительно для proximity:**
- После `AddOrReplaceStream` — регистрация в `activeAudioSources` dict (для ECS bridge)
- `MuteAll()`/`UnmuteAll()` — итерация по `hub.Streams`, установка `audioSource.mute`

**Почему НЕ наследование:** `PlaybackSourcesHub` — `readonly struct`, нельзя наследовать.
**Почему НЕ отдельная копия:** Ядро lifecycle логики (ConcurrentDictionary, create/dispose, Play/Stop/Reset) идентично — дублирование опасно.

---

## Верификация

### После каждой фазы:
1. Проект компилируется без ошибок в Unity
2. Proximity voice chat работает: подключение к Island Room, публикация mic, 3D spatial audio

### Полная верификация:
1. Два клиента на одном острове
2. Логи: `Initialized` → `Activated — publishing and listening with 3D spatial audio`
3. Слышно удалённых игроков, звук позиционирован в 3D
4. При Private/Community call → `Suppressed`, proximity замьючен
5. После звонка → `Resumed`, proximity восстановлен
6. Debug виджет: слайдеры работают (spatial blend, distance, rolloff)
7. Дисконнект от острова → `Deactivated`, cleanup

### Тесты:
- Поиск существующих: `grep -r "ProximityVoiceChatManager\|ProximityAudioPosition" --include="*Test*"`
- Для новых классов (`ProximityRemoteTrackListener`, `VoiceChatTrackPublishHelper`) — unit-тесты по паттерну `UnitySystemTestBase<T>`
