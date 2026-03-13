# ADR: Audio Effect Zones

**Status:** Accepted (design phase)
**Date:** 2026-03-13
**Authors:** Voice Chat team

---

## Context

Decentraland Explorer поддерживает Proximity Voice Chat (spatial audio, привязанный к аватарам через Island Room). Следующий шаг -- дать разработчикам сцен возможность определять **Audio Effect Zones** -- области, в которых звук (голос, мировые звуки, шаги аватара) подвергается модификации: reverb, echo, фильтрация, усиление или тишина.

### Требования

1. Сцена определяет зону через SDK-компонент (аналогично `AvatarModifierArea`, `CameraModeArea`)
2. Вся логика применения эффектов -- на стороне Unity-клиента (сцена не знает о репликах аватаров, шагах и прочих не-SDK звуках)
3. Зона влияет на **источники звука** (source-based) внутри неё по умолчанию
4. Итеративная разработка: от простого (Silence) к сложному (stacking, blending, transitions)

### Существующие зонные компоненты

| Компонент | ID | Паттерн | Отправляет события в SDK? |
|-----------|----|---------|--------------------------|
| `PBTriggerArea` | 1060 | Зона + `TriggerAreaResult` обратно в сцену | Да (ENTER/STAY/EXIT) |
| `PBAvatarModifierArea` | 1070 | Зона, Unity применяет эффект сам | Нет |
| `PBCameraModeArea` | 1071 | Зона, Unity применяет эффект сам | Нет |

Audio Effect Zones по семантике ближе к `AvatarModifierArea` -- Unity сам определяет затронутые объекты и применяет эффект.

---

## Decision

### Выбранный вариант: Standalone `PBAudioEffectZone` (гибрид A+B)

Один proto-компонент с `oneof effect` для типизированных эффектов. Каждый тип эффекта имеет собственный message с именованными параметрами. Зона использует существующую `SDKEntityTriggerArea` инфраструктуру для детекции объектов.

### Proto-определение

```protobuf
syntax = "proto3";
package decentraland.sdk.components;

import "decentraland/sdk/components/common/id.proto";
import "decentraland/common/vectors.proto";

option (common.ecs_component_id) = 1072;

message PBAudioEffectZone {
  decentraland.common.Vector3 area = 1;
  optional AudioEffectZoneMeshType mesh = 2;     // default: BOX
  repeated string exclude_ids = 3;

  oneof effect {
    ReverbEffect reverb = 10;
    EchoEffect echo = 11;
    FilterEffect filter = 12;
    AmplificationEffect amplification = 13;
    SilenceEffect silence = 14;
  }
}

message ReverbEffect {
  optional float decay_time = 1;       // seconds, default 1.0
  optional float wet_mix = 2;          // 0..1, default 0.5
  optional ReverbPreset preset = 3;
}

enum ReverbPreset {
  RP_SMALL_ROOM = 0;
  RP_LARGE_HALL = 1;
  RP_CAVE = 2;
  RP_CATHEDRAL = 3;
}

message EchoEffect {
  optional float delay = 1;            // ms, default 500
  optional float decay_ratio = 2;      // 0..1, default 0.5
}

message FilterEffect {
  FilterType filter_type = 1;
  optional float intensity = 2;        // 0..1, default 0.5
}

enum FilterType {
  FT_METALLIC = 0;
  FT_OPAQUE = 1;       // low-pass / muffled
  FT_WATERY = 2;
  FT_ROBOTIC = 3;
}

message AmplificationEffect {
  optional float volume_multiplier = 1;    // default 2.0
  optional float distance_multiplier = 2;  // default 2.0
}

message SilenceEffect {
  // Mutes all audio sources inside the zone
}

enum AudioEffectZoneMeshType {
  AEZMT_BOX = 0;
  AEZMT_SPHERE = 1;
}
```

### Архитектура на Unity-стороне

```mermaid
flowchart TD
    Scene["SDK Scene"] -->|CRDT| PB["PBAudioEffectZone"]
    PB -->|ECS| Setup["AudioEffectZoneHandlerSystem.Setup"]
    Setup -->|создаёт| SETA["SDKEntityTriggerAreaComponent"]
    Setup -->|создаёт| AEZC["AudioEffectZoneComponent"]
    SETA -->|"SDKEntityTriggerAreaHandlerSystem"| Collider["SDKEntityTriggerArea MonoBehaviour"]
    Collider -->|"OnTriggerEnter/Exit"| Update["AudioEffectZoneHandlerSystem.Update"]
    Update -->|"FindAvatarUtils.AvatarWithTransform"| Avatar["Entity с ProximityAudioSourceComponent"]
    Avatar -->|"AudioSource"| Effect["Применение эффекта"]
    Effect -->|"mute / AudioReverbFilter / AudioEchoFilter / etc."| AudioSource["Unity AudioSource"]
```

### Поток данных

1. Сцена добавляет `PBAudioEffectZone` на entity через CRDT
2. `AudioEffectZoneHandlerSystem.Setup` создаёт `SDKEntityTriggerAreaComponent` + `AudioEffectZoneComponent`
3. `SDKEntityTriggerAreaHandlerSystem` привязывает Box/Sphere коллайдер из пула
4. Unity Physics вызывает `OnTriggerEnter`/`OnTriggerExit` на `SDKEntityTriggerArea`
5. `AudioEffectZoneHandlerSystem.Update` читает entered/exited, через `FindAvatarUtils.AvatarWithTransform` находит Entity аватара
6. Через Entity в `globalWorld` получает `ProximityAudioSourceComponent` с `AudioSource`
7. Применяет/снимает эффект (mute, AudioFilter, параметры rolloff)

### Ключевые решения

**1. `oneof` вместо `repeated` (первая итерация)**

- Одна зона = один эффект. Проще реализация, проще отладка
- Миграция на `repeated` запланирована на итерацию 8

**2. Source-based по умолчанию**

- Зона влияет на источники звука (аватары) внутри неё
- Listener-based (игрок внутри зоны слышит всё иначе) -- отдельная категория, см. NOTES
- Per-effect решение на стороне Unity-клиента

**3. Стекинг: last-wins -> priority + blend**

- Итерации 1-4: последняя зона перезаписывает предыдущую
- Итерация 7: приоритет по типу эффекта + blending для одинаковых типов
- Приоритеты определяются Unity-кодом, не proto

**4. Без `collision_mask` в первой итерации**

- Unity-клиент сам определяет цели (голоса, мировые звуки, шаги)
- `audio_target_mask` добавится в итерации 5

**5. Без `fade_time` в первой итерации**

- Мгновенное включение/выключение эффекта
- Плавные переходы добавятся в итерации 6

---

## Alternatives Considered

### Вариант B: Множество компонентов (`PBAudioSilenceZone`, `PBAudioReverbZone`, ...)

Отдельный proto-компонент на каждый тип эффекта.

| За | Против |
|----|--------|
| Строго типизированные параметры | Дублирование `area`, `mesh`, `exclude_ids` в каждом proto |
| Отдельные системы с чёткой ответственностью | Много component ID, много систем, много регистраций |
| | Пересечение зон разных типов сложнее приоритизировать |

**Отклонён:** дублирование и рост количества компонентов перевешивают преимущества. `oneof` в одном proto решает задачу типизации.

### Вариант C: `PBTriggerArea` + `PBAudioEffect` на одной entity

Переиспользовать `TriggerArea` для зонирования, `PBAudioEffect` -- описание эффекта.

| За | Против |
|----|--------|
| Максимальная переиспользуемость | Неявная связь ("если оба компонента есть -- это audio zone") |
| Минимум нового кода в SDK | `TriggerArea` шлёт `TriggerAreaResult` в SDK -- лишний CRDT-трафик |
| Композиция на одной entity | Сцена получает enter/exit и может конфликтовать с Unity-логикой |

**Отклонён:** неявная связь хрупкая; лишний CRDT-трафик; coupling с другими системами.

### Вариант D: Полностью клиентский (без proto)

Unity-only компоненты и системы, без SDK-определения.

**Отклонён:** сцены не могут определять зоны, теряется весь смысл SDK-интеграции.

---

## Technical Details

### Ключевые зависимости (Unity)

| Файл / Класс | Роль |
|---------------|------|
| `SDKEntityTriggerArea` | MonoBehaviour с Box/Sphere коллайдерами, OnTriggerEnter/Exit |
| `SDKEntityTriggerAreaComponent` | ECS-компонент, хранит ссылку на MonoBehaviour и размер зоны |
| `SDKEntityTriggerAreaHandlerSystem` | Привязка коллайдеров из пула к Transform entity |
| `SDKEntityTriggerAreaCleanupSystem` | Очистка; нужно добавить `PBAudioEffectZone` в `[None(...)]` |
| `FindAvatarUtils.AvatarWithTransform` | Маппинг Collider -> Entity аватара |
| `ProximityAudioSourceComponent` | Хранит `AudioSource` для proximity voice |
| `AvatarModifierAreaHandlerSystem` | Паттерн-образец для zone handler system |

### Ключевые зависимости (SDK)

| Файл / Путь | Роль |
|-------------|------|
| `protocol/proto/decentraland/sdk/components/audio_effect_zone.proto` | Определение proto |
| `protocol/public/sdk-components.proto` | Регистрация импорта |
| `js-sdk-toolchain/packages/@dcl/ecs/src/components/extended/AudioEffectZone.ts` | SDK-хелперы |

### Audio Pipeline и DSP

```
LiveKit decode (Opus mono) -> AudioStream (stereo upmix)
  -> OnAudioFilterRead:
    -> SpatialAudioDSP (ITD, ILD, HeadShadow, HRTF)
    -> Unity AudioFilters (ReverbFilter, EchoFilter, LowPassFilter)
  -> AudioMixer (VoiceChat group)
  -> Speakers
```

Unity AudioFilter компоненты добавляются на тот же GameObject, что и `AudioSource`. Они работают **после** `OnAudioFilterRead` (LiveKit spatial DSP) и **до** AudioMixer. Для Silence Zone достаточно `AudioSource.mute = true`.

---

## Consequences

### Positive

- Один компонент покрывает все типы эффектов через `oneof`
- Полное соответствие паттерну `AvatarModifierArea` / `CameraModeArea`
- Переиспользование `SDKEntityTriggerArea` -- минимум новой инфраструктуры
- Вся логика на Unity-стороне -- сцена не знает о репликах, шагах, мировых звуках
- Итеративная разработка: каждая итерация самодостаточна

### Negative / Risks

- `oneof` ограничивает одной зоной один эффект (до миграции на `repeated`)
- Стекинг зон в первой итерации примитивный (last-wins)
- Добавление Unity AudioFilter компонентов на runtime может вызвать аллокации
- Нужно тестировать взаимодействие AudioFilter с LiveKit DSP pipeline

### Future Considerations

- Миграция `oneof` -> `repeated` для нескольких эффектов на зону
- Listener-based зоны (обработка на AudioListener)
- `audio_target_mask` для фильтрации по типу источника
- `fade_time` для плавных переходов
- `priority` в proto для серверной приоритизации
