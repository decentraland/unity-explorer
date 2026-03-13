# Audio Effect Zones -- Plan

## Context

SDK-определяемые зоны, модифицирующие аудио (голоса, мировые звуки, шаги) для объектов внутри.
Реализация идёт итеративно от минимального PoC до полной системы.

**ADR:** [ADR_audio_effect_zones.md](ADR_audio_effect_zones.md)
**Notes:** [NOTES_audio_effect_zones.md](NOTES_audio_effect_zones.md)

---

## Iteration 1: Silence Zone (PoC)

**Status:** Planned

Минимальный proof-of-concept. Одна зона тишины, мгновенное переключение, без наворотов.

### Scope

- **Proto:** `PBAudioEffectZone` с `oneof`, реализован только `SilenceEffect`
- **Без:** transition, collision_mask, приоритетов, repeated, stacking
- **Стекинг:** last-wins (последняя зона перезаписывает)

### Что делать

#### SDK / Protocol

1. Создать `protocol/proto/decentraland/sdk/components/audio_effect_zone.proto`
   - Полный proto из ADR (все messages и enums), но Unity реализует только `SilenceEffect`
   - ID: 1072
2. Добавить импорт в `protocol/public/sdk-components.proto`:
   ```protobuf
   import public "decentraland/sdk/components/audio_effect_zone.proto";
   ```
3. Создать extended-хелпер `packages/@dcl/ecs/src/components/extended/AudioEffectZone.ts`:
   ```typescript
   AudioEffectZone.setSilence(entity, excludeIds?)
   ```

#### Unity -- Protocol

4. Сгенерировать `PBAudioEffectZone.gen.cs` (protobuf codegen)
5. Зарегистрировать в `ComponentsContainer.cs`:
   ```csharp
   .Add(SDKComponentBuilder<PBAudioEffectZone>.Create(ComponentID.AUDIO_EFFECT_ZONE).AsProtobufComponent())
   ```

#### Unity -- ECS Components

6. Создать `Explorer/Assets/DCL/SDKComponents/AudioEffectZone/Components/AudioEffectZoneComponent.cs`:
   ```csharp
   public struct AudioEffectZoneComponent
   {
       public readonly HashSet<Entity> AffectedEntities;
       // ... конструктор, Dispose (возврат в pool)
   }
   ```

#### Unity -- ECS System

7. Создать `Explorer/Assets/DCL/SDKComponents/AudioEffectZone/Systems/AudioEffectZoneHandlerSystem.cs`:

   По паттерну `AvatarModifierAreaHandlerSystem`:

   ```
   AudioEffectZoneHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
   ├── [UpdateInGroup(SyncedInitializationFixedUpdateThrottledGroup)]
   │
   ├── Setup query:
   │   ├── [None(SDKEntityTriggerAreaComponent, AudioEffectZoneComponent)]
   │   ├── [All(TransformComponent, PBAudioEffectZone)]
   │   └── World.Add(entity, SDKEntityTriggerAreaComponent + AudioEffectZoneComponent)
   │
   ├── Update query:
   │   ├── [All(TransformComponent, PBAudioEffectZone, SDKEntityTriggerAreaComponent, AudioEffectZoneComponent)]
   │   ├── Entered: FindAvatarUtils.AvatarWithTransform → globalWorld.TryGet<ProximityAudioSourceComponent>
   │   │   └── AudioSource.mute = true; affectedEntities.Add(entity)
   │   ├── Exited: unmute; affectedEntities.Remove(entity)
   │   └── IsDirty: обновить размер зоны
   │
   ├── HandleEntityDestruction query:
   │   ├── [All(DeleteEntityIntention, PBAudioEffectZone, AudioEffectZoneComponent)]
   │   └── Unmute all affected → Dispose → Remove component
   │
   ├── HandleComponentRemoval query:
   │   ├── [None(DeleteEntityIntention, PBAudioEffectZone)]
   │   ├── [All(AudioEffectZoneComponent)]
   │   └── Unmute all affected → Dispose → Remove component
   │
   └── FinalizeComponents:
       └── Unmute all affected → Dispose
   ```

   **Зависимости конструктора:** `World world`, `World globalWorld`

#### Unity -- Integration

8. Добавить `PBAudioEffectZone` в `[None(...)]` фильтр cleanup-запросов в `SDKEntityTriggerAreaCleanupSystem.cs`:
   ```csharp
   [Query]
   [None(typeof(PBTriggerArea), typeof(PBCameraModeArea), typeof(PBAvatarModifierArea), typeof(PBAudioEffectZone))]
   private void HandleComponentRemoval(...)
   ```

9. Подключить систему в plugin (по паттерну `SDKEntityTriggerAreaPlugin`):
   - Зарегистрировать `AudioEffectZoneHandlerSystem` как `IFinalizeWorldSystem`

#### Testing

10. Тестовая сцена в `sdk7-test-scenes`:
    - Entity с Transform (scale = зона) + PBAudioEffectZone (SilenceEffect)
    - MeshRenderer для визуализации зоны (полупрозрачный куб)
    - Войти в зону, проверить mute proximity voice

### Files

| Action | Path |
|--------|------|
| Create | `protocol/proto/decentraland/sdk/components/audio_effect_zone.proto` |
| Modify | `protocol/public/sdk-components.proto` |
| Create | `js-sdk-toolchain/packages/@dcl/ecs/src/components/extended/AudioEffectZone.ts` |
| Create | `Explorer/Assets/DCL/SDKComponents/AudioEffectZone/Components/AudioEffectZoneComponent.cs` |
| Create | `Explorer/Assets/DCL/SDKComponents/AudioEffectZone/Systems/AudioEffectZoneHandlerSystem.cs` |
| Modify | `Explorer/Assets/DCL/Infrastructure/Global/ComponentsContainer.cs` |
| Modify | `Explorer/Assets/DCL/SDKEntityTriggerArea/Systems/SDKEntityTriggerAreaCleanupSystem.cs` |
| Modify | Plugin file (registration) |

---

## Iteration 2: Reverb Effect

**Status:** Planned

### Scope

- Добавить обработку `ReverbEffect` в `AudioEffectZoneHandlerSystem`
- Unity: при enter -- добавить `AudioReverbFilter` на AudioSource GameObject; при exit -- снять

### Details

- `ReverbEffect.preset` -> `AudioReverbPreset` маппинг:
  | Proto | Unity |
  |-------|-------|
  | `RP_SMALL_ROOM` | `AudioReverbPreset.Room` |
  | `RP_LARGE_HALL` | `AudioReverbPreset.ConcertHall` |
  | `RP_CAVE` | `AudioReverbPreset.Cave` |
  | `RP_CATHEDRAL` | `AudioReverbPreset.Arena` |
- `ReverbEffect.decay_time` -> `AudioReverbFilter.decayTime`
- `ReverbEffect.wet_mix` -> `AudioReverbFilter.dryLevel` / `AudioReverbFilter.room`
- При exit -- `Destroy(audioReverbFilter)` или `enabled = false`

### Notes

- `AudioReverbFilter` добавляется на runtime -> minor allocation
- Порядок фильтров: LiveKit DSP (OnAudioFilterRead) -> AudioReverbFilter -> AudioMixer
- Тестировать: не ломает ли reverb spatial DSP pipeline

### Files

| Action | Path |
|--------|------|
| Modify | `Explorer/Assets/DCL/SDKComponents/AudioEffectZone/Systems/AudioEffectZoneHandlerSystem.cs` |

---

## Iteration 3: Echo + Filter Effects

**Status:** Planned

### Scope

- `EchoEffect` -> Unity `AudioEchoFilter`
- `FilterEffect` -> Unity `AudioLowPassFilter` / `AudioHighPassFilter` / custom DSP

### Details

#### Echo
- `EchoEffect.delay` -> `AudioEchoFilter.delay` (ms)
- `EchoEffect.decay_ratio` -> `AudioEchoFilter.decayRatio`

#### Filter
- `FT_OPAQUE` (muffled) -> `AudioLowPassFilter` с cutoff ~1000 Hz
- `FT_METALLIC` -> `AudioHighPassFilter` + resonance boost
- `FT_WATERY` -> `AudioLowPassFilter` с modulated cutoff (LFO)
- `FT_ROBOTIC` -> ring modulation или vocoder-like effect (custom DSP в OnAudioFilterRead)

### Notes

- WATERY и ROBOTIC могут потребовать расширения LiveKit DSP pipeline или отдельного AudioFilter MonoBehaviour
- `intensity` управляет wet/dry mix эффекта

### Files

| Action | Path |
|--------|------|
| Modify | `AudioEffectZoneHandlerSystem.cs` |
| Create | Custom filter MonoBehaviours (если нужны) |

---

## Iteration 4: Amplification Effect

**Status:** Planned

### Scope

- `AmplificationEffect` -> изменение rolloff-параметров AudioSource

### Details

- `volume_multiplier` -> `AudioSource.volume *= multiplier`
- `distance_multiplier` -> `AudioSource.maxDistance *= multiplier`
- Нужно сохранять оригинальные значения для восстановления при exit
- Отличие от DSP-эффектов: не фильтр, а параметры пространственного звука

### Notes

- Сохранение оригинальных параметров: добавить в `AudioEffectZoneComponent` или отдельный struct
- Amplification влияет на то, как далеко слышно аватара -- "микрофон"

### Files

| Action | Path |
|--------|------|
| Modify | `AudioEffectZoneHandlerSystem.cs` |
| Modify | `AudioEffectZoneComponent.cs` (хранение оригинальных параметров) |

---

## Iteration 5: Audio Target Mask

**Status:** Planned

### Scope

- Добавить `optional uint32 audio_target_mask` в `PBAudioEffectZone` proto
- Определить enum `AudioTargetType` (VOICE, AVATAR_SOUNDS, WORLD_AUDIO, ALL)
- Unity: фильтрация по типу AudioSource при применении эффекта

### Details

```protobuf
enum AudioTargetType {
  ATT_ALL = 0;
  ATT_VOICE = 1;
  ATT_AVATAR_SOUNDS = 2;
  ATT_WORLD_AUDIO = 4;
}

// В PBAudioEffectZone:
optional uint32 audio_target_mask = 4;  // bitmask, default ATT_ALL
```

### Notes

- Требует категоризации AudioSource на Unity-стороне (голос vs. мировой звук vs. шаги)
- `ProximityAudioSourceComponent` -- голоса; SDK `AudioSource` -- мировые звуки

### Files

| Action | Path |
|--------|------|
| Modify | `protocol/.../audio_effect_zone.proto` |
| Modify | `AudioEffectZoneHandlerSystem.cs` |

---

## Iteration 6: Transition (Fade In / Fade Out)

**Status:** Planned

### Scope

- Добавить `optional float fade_time` в `PBAudioEffectZone` proto
- Unity: lerp параметров при enter/exit

### Details

- Silence: fade `AudioSource.volume` от 1 -> 0 (enter) и 0 -> 1 (exit)
- Reverb: fade `wetMix` от 0 -> target (enter) и target -> 0 (exit)
- Echo: fade `wetMix` аналогично
- Amplification: fade `volume_multiplier` и `distance_multiplier` от 1 -> target
- Требует per-entity tracking текущего состояния transition (elapsed time, direction)

### Notes

- Transition state хранить в отдельном компоненте или расширить `AudioEffectZoneComponent`
- Update должен обрабатывать `CurrentEntitiesInside` каждый кадр для активных transitions
- `fade_time = 0` -> мгновенное переключение (обратная совместимость)

### Files

| Action | Path |
|--------|------|
| Modify | `protocol/.../audio_effect_zone.proto` |
| Modify | `AudioEffectZoneHandlerSystem.cs` |
| Modify/Create | Transition state component |

---

## Iteration 7: Priority + Blending

**Status:** Planned

### Scope

- Unity-side приоритет по типу эффекта
- Blending для нескольких зон одного типа

### Details

**Priority (Unity-side, не в proto):**
```
Silence (highest) > Amplification > Filter > Reverb > Echo (lowest)
```

**Стекинг:**
- Аватар в нескольких зонах -> применяется эффект с наивысшим приоритетом
- Несколько зон одного типа -> blend параметров (среднее / max / weighted)

**Реализация:**
- Компонент на аватаре: `AudioEffectActiveZones` -- список активных зон с параметрами
- При enter/exit пересчитывать итоговый эффект
- Возможно добавить `optional int32 priority` в proto для тонкой настройки

### Notes

- Переход от "применить/снять" к "пересчитать итоговый стек"
- Значительный рефакторинг handler-системы
- Альтернатива: приоритет из proto позволяет сценам управлять порядком

### Files

| Action | Path |
|--------|------|
| Modify | `AudioEffectZoneHandlerSystem.cs` (stacking logic) |
| Create | `AudioEffectActiveZonesComponent.cs` (per-avatar component) |
| Possibly modify | `protocol/.../audio_effect_zone.proto` (priority field) |

---

## Iteration 8: Repeated Effects

**Status:** Planned

### Scope

- Миграция proto: `oneof effect` -> `repeated AudioEffect effects`
- Unity: цепочка эффектов на одном AudioSource

### Details

```protobuf
message AudioEffect {
  oneof effect {
    ReverbEffect reverb = 1;
    EchoEffect echo = 2;
    FilterEffect filter = 3;
    AmplificationEffect amplification = 4;
    SilenceEffect silence = 5;
  }
}

// В PBAudioEffectZone замена:
// oneof effect { ... }  ->  repeated AudioEffect effects = 10;
```

- Порядок в массиве = порядок в DSP chain
- Приоритет вычисляется по наивысшему эффекту в массиве
- Обратная совместимость: если `effects` пуст, проверить `oneof` поля (deprecation)

### Notes

- Breaking change в proto -- требует координации с SDK командой
- Сложность стекинга возрастает: per-zone chain + inter-zone priority

### Files

| Action | Path |
|--------|------|
| Modify | `protocol/.../audio_effect_zone.proto` |
| Modify | `AudioEffectZoneHandlerSystem.cs` |
| Modify | SDK extended component |
