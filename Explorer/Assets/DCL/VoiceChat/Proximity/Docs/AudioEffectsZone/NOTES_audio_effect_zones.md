# Audio Effect Zones -- Notes

Заметки, edge cases и технические детали для реализации Audio Effect Zones.

**ADR:** [ADR_audio_effect_zones.md](ADR_audio_effect_zones.md)
**Plan:** [PLAN_audio_effect_zones.md](PLAN_audio_effect_zones.md)

---

## Source-based vs Listener-based Zones

### Source-based (default)

Зона влияет на **источники звука** (аватары, AudioSource), находящиеся внутри неё.
Все слушатели воспринимают изменённый звук одинаково.

**Примеры:**
- Reverb на стадионе -- голос аватара на сцене звучит с реверберацией для всех
- Silence zone -- аватар внутри замолкает для всех слушателей
- Amplification -- аватар у микрофона слышен дальше

### Listener-based

Зона влияет на **слушателя** (локального игрока) внутри неё.
Только игрок в зоне слышит изменённый звук.

**Практичные примеры:**
- **Noise-cancelling room** (Silence/listener): игрок в комнате не слышит звуки извне -- приватное пространство, медитация
- **Underwater zone** (Filter/listener): всё звучит приглушённо, low-pass на всех входящих потоках -- бассейн, подводная локация
- **Heightened hearing** (Amplification/listener): игрок слышит дальше обычного -- сторожевая вышка, магический эффект
- **Concert hall** (Reverb/listener): реверберация на всём, что слышит игрок -- архитектурная акустика

**Интересные / game-design примеры:**
- **Psychedelic zone**: все звуки искажены для игрока внутри -- арт-инсталляция, трип
- **Time-warp zone**: pitch shift на всех звуках -- замедление/ускорение времени
- **Whisper zone** (partial silence/listener): звуки извне приглушены но не выключены -- библиотека, музей
- **DJ booth**: игрок в зоне слышит другой audio mix -- VIP-зона на вечеринке
- **Selective hearing**: только голоса усилены, мировые звуки приглушены -- фокус на разговоре

### Реализация listener-based

Listener-based требует обработки на уровне `AudioListener` или модификации **всех** входящих AudioSource для данного игрока. Варианты:

1. **AudioListener filter:** добавить AudioFilter на объект AudioListener (камера). Влияет на весь аудио-микс
2. **Per-source modification:** при входе игрока в listener-зону модифицировать все AudioSource, которые он слышит
3. **AudioMixer snapshot:** переключить mixer snapshot при входе в зону

В первых итерациях реализуем только source-based. Listener-based -- отдельная фича после итерации 8.

---

## Audio DSP Pipeline

### Порядок обработки

```
[LiveKit native decode] Opus mono -> stereo upmix
         |
         v
[OnAudioFilterRead] LivekitAudioSource.cs
  1. AudioStream.ReadAudio() -- PCM data
  2. ComputeLipSyncAmplitudes() -- RMS, bandpass, Goertzel
  3. SpatialAudioDSP.Process() -- ITD, ILD, HeadShadow, HRTF
         |
         v
[Unity AudioFilter chain] (добавляются на тот же GameObject)
  - AudioReverbFilter
  - AudioEchoFilter
  - AudioLowPassFilter
  - AudioHighPassFilter
  - Custom MonoBehaviour filters
         |
         v
[AudioMixer] VoiceChat group
  - VoiceChat_Volume
  - Microphone_Volume
         |
         v
[AudioListener] -> Speakers
```

### Ключевые моменты

1. **LiveKit DSP идёт первым** -- spatial positioning (HRTF, ILD, ITD) применяется в `OnAudioFilterRead`. Unity Filters работают уже с пространственно-обработанным сигналом. Это корректно: reverb/echo на spatialized audio = реалистичная акустика.

2. **Unity AudioSource.spatialBlend = 1.0** для proximity voice. AudioFilters работают в local space AudioSource. При spatialBlend=1 Unity сам применяет distance attenuation и rolloff **до** фильтров.

3. **Порядок фильтров** определяется порядком компонентов на GameObject. При добавлении нескольких фильтров контролировать порядок через `AddComponent` sequence.

4. **Для Silence** (`AudioSource.mute = true`) -- пропускает весь pipeline, максимально эффективно.

5. **OnAudioFilterRead thread** -- вызывается на audio thread, не на main thread. Добавление/удаление AudioFilter компонентов -- на main thread. Thread-safe, потому что Unity синхронизирует добавление MonoBehaviour с audio thread.

---

## SDKEntityTriggerArea Integration

### Как добавить новый зонный компонент

Паттерн из `AvatarModifierArea` и `CameraModeArea`:

1. **Handler system** создаёт `SDKEntityTriggerAreaComponent` в Setup-запросе
2. **SDKEntityTriggerAreaHandlerSystem** (уже существует) привязывает коллайдер из пула при `IsDirty`
3. **SDKEntityTriggerAreaCleanupSystem** -- нужно добавить `PBAudioEffectZone` в `[None(...)]` фильтр, чтобы cleanup-система **не** удаляла `SDKEntityTriggerAreaComponent` до обработки в handler

### SDKEntityTriggerAreaComponent creation

```csharp
World.Add(entity,
    new SDKEntityTriggerAreaComponent(
        areaSize: pbAudioEffectZone.Area,
        targetOnlyMainPlayer: false  // зона действует на всех аватаров
    ),
    new AudioEffectZoneComponent(...)
);
```

`targetOnlyMainPlayer: false` -- зона реагирует на всех аватаров (как AvatarModifierArea).
`ColliderLayer.ClPlayer` -> слой `PhysicsLayers.ALL_AVATARS`.

### Чтение entered/exited

```csharp
ref var triggerArea = ref sdkEntityTriggerAreaComponent;
foreach (Collider collider in triggerArea.EnteredEntitiesToBeProcessed)
{
    var result = FindAvatarUtils.AvatarWithTransform(globalWorld, collider.transform);
    if (!result.Success) continue;
    Entity avatarEntity = result.Value;
    // apply effect...
}
triggerArea.TryClearEnteredAvatarsToBeProcessed();
```

### Cleanup важность

При удалении entity или снятии `PBAudioEffectZone`:
1. **Unmute / снять фильтры** со всех affected аватаров
2. **Dispose** `AudioEffectZoneComponent` (вернуть HashSet в pool)
3. **Remove** component с entity

Если не снять эффект -- аватар останется muted навсегда.

---

## FindAvatarUtils -> ProximityAudioSourceComponent

### Цепочка: Collider -> AudioSource

```
SDKEntityTriggerArea.OnTriggerEnter(Collider other)
         |
         v
FindAvatarUtils.AvatarWithTransform(globalWorld, other.transform)
         |  (ищет Entity с AvatarBase по иерархии Transform)
         v
Entity avatarEntity  (в globalWorld)
         |
         v
globalWorld.TryGet<ProximityAudioSourceComponent>(avatarEntity, out var proximityAudio)
         |
         v
proximityAudio.AudioSource  (Unity AudioSource для proximity voice)
```

### Edge cases

- **Аватар без ProximityAudioSourceComponent** -- ещё не подключен к voice chat. Пропустить, эффект применится когда компонент появится (нужна проверка в AssignPendingSources или отдельная sync-система)
- **Аватар с exclude_id** -- проверить userId через `AvatarShapeComponent` или Profile
- **Свой аватар** -- не применять эффект к собственному AudioSource (у локального игрока нет proximity AudioSource). Но для Silence source-based: свой голос не слышен другим -- это ок. Для listener-based Silence: нужен отдельный подход
- **AudioSource уже destroyed** -- null-check обязателен

---

## Stacking Evolution

### Iteration 1-4: Last-wins

```
Аватар входит в Zone_A (Reverb) -> применить Reverb
Аватар входит в Zone_B (Silence) -> снять Reverb, применить Silence
Аватар выходит из Zone_B -> снять Silence, применить... что?
```

**Проблема:** при выходе из Zone_B нужно знать, что аватар всё ещё в Zone_A.

**Решение для first iterations:** хранить `List<Entity>` активных зон на аватаре. При exit из верхней зоны -- переприменить предыдущую. Это уже простой stack, не чистый last-wins. Минимальная реализация:

```csharp
public struct AudioEffectActiveZones
{
    public List<Entity> ZoneStack;  // ordered by entry time
}
```

При enter: push zone. При exit: remove zone, reapply top.

### Iteration 7: Priority + Blend

- Сортировка ZoneStack по priority вместо entry time
- Priority определяется типом: Silence(100) > Amplification(80) > Filter(60) > Reverb(40) > Echo(20)
- Blend для одного типа: если два Reverb -- средний decay_time и wet_mix

---

## Performance Considerations

### Аллокации

- `AddComponent<AudioReverbFilter>()` -- аллокация при входе в зону. Для voice chat (десятки, не тысячи аватаров) -- приемлемо
- `HashSet<Entity>` в `AudioEffectZoneComponent` -- из пула (`HashSetPool<Entity>`)
- Не использовать LINQ в Update
- `FindAvatarUtils.AvatarWithTransform` внутри использует `World.Query` -- overhead на каждый entered collider. Для малого числа аватаров -- ок

### Throttling

Система в `SyncedInitializationFixedUpdateThrottledGroup` -- обновляется не каждый кадр.
Для мгновенных эффектов (mute/unmute) задержка в 1-2 кадра приемлема.
Для transitions (fade) может потребоваться более частое обновление.

### Destroy vs Disable

При exit из зоны:
- `Destroy(audioReverbFilter)` -- GC pressure
- `audioReverbFilter.enabled = false` -- нет аллокации, но компонент остаётся на GameObject

Рекомендация: для первых итераций `Destroy`. Для оптимизации -- пул AudioFilter компонентов или `enabled` toggle.

---

## Proto Component ID

Выбранный ID: **1072** (следующий после CameraModeArea = 1071).

Диапазон 12xx -- основные компоненты. 14xx -- экспериментальные. 1072 вписывается в ряд зонных компонентов:

| ID | Component |
|----|-----------|
| 1060 | PBTriggerArea |
| 1061 | PBTriggerAreaResult |
| 1070 | PBAvatarModifierArea |
| 1071 | PBCameraModeArea |
| **1072** | **PBAudioEffectZone** |

Перед финальным выбором ID -- проверить нет ли конфликтов с другими командами.

---

## SDK Usage Example (target DX)

```typescript
import { engine, Transform, AudioEffectZone } from '@dcl/sdk/ecs'
import { Vector3 } from '@dcl/sdk/math'

// Silence zone -- mutes all voices inside
const silenceZone = engine.addEntity()
Transform.create(silenceZone, {
  position: Vector3.create(8, 1, 8),
  scale: Vector3.create(4, 3, 4)
})
AudioEffectZone.setSilence(silenceZone)

// Reverb zone -- cathedral effect
const reverbZone = engine.addEntity()
Transform.create(reverbZone, {
  position: Vector3.create(16, 2, 16),
  scale: Vector3.create(10, 6, 10)
})
AudioEffectZone.setReverb(reverbZone, {
  preset: ReverbPreset.RP_CATHEDRAL,
  wetMix: 0.7
})

// Amplification zone -- microphone effect
const micZone = engine.addEntity()
Transform.create(micZone, {
  position: Vector3.create(24, 1, 24),
  scale: Vector3.create(2, 2, 2)
})
AudioEffectZone.setAmplification(micZone, {
  volumeMultiplier: 3.0,
  distanceMultiplier: 4.0
})
```

---

## Reference Files

### Unity Explorer

| File | Purpose |
|------|---------|
| `Explorer/Assets/DCL/SDKComponents/AvatarModifierArea/Systems/AvatarModifierAreaHandlerSystem.cs` | Pattern: zone handler system |
| `Explorer/Assets/DCL/SDKComponents/AvatarModifierArea/Components/AvatarModifierAreaComponent.cs` | Pattern: zone ECS component |
| `Explorer/Assets/DCL/SDKComponents/CameraControl/CameraModeArea/Systems/CameraModeAreaHandlerSystem.cs` | Pattern: zone handler (targetOnlyMainPlayer) |
| `Explorer/Assets/DCL/SDKEntityTriggerArea/SDKEntityTriggerArea.cs` | Trigger area MonoBehaviour |
| `Explorer/Assets/DCL/SDKEntityTriggerArea/Components/SDKEntityTriggerAreaComponent.cs` | Trigger area ECS component |
| `Explorer/Assets/DCL/SDKEntityTriggerArea/Systems/SDKEntityTriggerAreaHandlerSystem.cs` | Assigns colliders from pool |
| `Explorer/Assets/DCL/SDKEntityTriggerArea/Systems/SDKEntityTriggerAreaCleanupSystem.cs` | Cleanup (add [None] filter) |
| `Explorer/Assets/DCL/AvatarRendering/AvatarShape/Systems/FindAvatarUtils.cs` | Collider -> Entity mapping |
| `Explorer/Assets/DCL/Infrastructure/CrdtEcsBridge/Physics/PhysicsLayers.cs` | Physics layers |
| `Explorer/Assets/DCL/Infrastructure/Global/ComponentsContainer.cs` | Component registration |
| `Explorer/Assets/DCL/VoiceChat/Proximity/ProximityAudioSourceComponent.cs` | AudioSource per avatar |
| `Explorer/Assets/DCL/VoiceChat/Proximity/Systems/ProximityAudioPositionSystem.cs` | Positions audio sources |
| `Explorer/Assets/DCL/VoiceChat/Proximity/ProximityVoiceChatManager.cs` | LiveKit track lifecycle |

### SDK / Protocol

| File | Purpose |
|------|---------|
| `protocol/proto/decentraland/sdk/components/avatar_modifier_area.proto` | Pattern: zone proto |
| `protocol/proto/decentraland/sdk/components/trigger_area.proto` | Pattern: trigger area proto |
| `protocol/proto/decentraland/sdk/components/audio_source.proto` | Existing audio component |
| `protocol/proto/decentraland/sdk/components/mesh_collider.proto` | ColliderLayer enum |
| `protocol/public/sdk-components.proto` | Component index |
| `js-sdk-toolchain/packages/@dcl/ecs/src/components/extended/AudioSource.ts` | Pattern: extended component |
| `js-sdk-toolchain/packages/@dcl/ecs/src/components/extended/TriggerArea.ts` | Pattern: extended component |

### LiveKit SDK

| File | Purpose |
|------|---------|
| `client-sdk-unity/Runtime/Scripts/LivekitAudioSource.cs` | OnAudioFilterRead, DSP |
| `client-sdk-unity/Runtime/Scripts/SpatialAudioDSP.cs` | Spatial DSP pipeline |
