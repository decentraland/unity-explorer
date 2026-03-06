# ADR: Streaming AudioClip для Proximity Voice Chat

**Status:** Accepted  
**Date:** 2026-03-06  
**Authors:** Voice Chat team  
**Supersedes:** Частично итерацию 2 из ADR_proximity_voice_chat (способ доставки аудио)

---

## Context

В итерации 2 Proximity Voice Chat реализован 3D spatial audio через `LivekitAudioSource` из LiveKit SDK. Этот компонент использует `OnAudioFilterRead` для инъекции аудио данных в Unity audio pipeline.

**Обнаруженная проблема:** `panStereo` (2D стерео-панорама) не работает. При `spatialBlend = 0` и полном смещении pan влево/вправо звук идёт одинаково в оба канала. При этом обычный AudioSource с клипом работает корректно.

**Эмпирическое тестирование** показало:
- `spatialBlend = 1` (3D) + rolloff — **работает** ✓
- `panStereo` при `spatialBlend = 0` (2D) — **не работает** ✗

---

## Analysis

### Порядок DSP-пайплайна Unity

Точного официального описания порядка Unity не публиковала. На основе эмпирических тестов и документации Native Audio Plugin SDK установлен следующий порядок:

```
AudioClip PCM (или тишина если нет клипа)
    ↓
Source-level: panStereo, volume, pitch                   [1]
    ↓
DSP Filter chain: OnAudioFilterRead + Audio Effects      [2]
    ↓
3D Spatialization (spatialBlend, rolloff, spread)        [3]
    ↓
AudioMixer → Output
```

### Почему OnAudioFilterRead ломает panStereo

`LivekitAudioSource.New()` создаёт AudioSource **без клипа**. Пайплайн при этом:

1. **[1]** AudioSource применяет panStereo к тишине (нет клипа) → тишина
2. **[2]** `OnAudioFilterRead` перезаписывает буфер сырыми данными LiveKit (стерео, L=R)
3. **[3]** 3D Spatialization применяется к данным LiveKit → работает

panStereo был применён к тишине на этапе [1] и перезаписан на этапе [2]. Этап [3] (3D) происходит после фильтров, поэтому работает.

### Дополнительные ограничения OnAudioFilterRead

- **Audio Effects** (AudioLowPassFilter, AudioReverbFilter и т.д.) — работают только если добавлены **после** `LivekitAudioSource` в списке компонентов. Неявная зависимость от порядка `AddComponent` — хрупко.
- **Стерео вход** — LiveKit `AudioStream` запрашивает 2 канала (`currentChannels = 2`). Моно-голос дублируется в оба канала. Для 3D spatializer моно-источник каноничнее.
- **Reverb Zones** — поведение не определено, зависит от реализации в конкретной версии Unity.

---

## Decision

### Заменить OnAudioFilterRead на streaming AudioClip

Вместо инъекции аудио через `OnAudioFilterRead` (этап [2] пайплайна), подавать данные через `AudioClip.Create(stream: true)` с `PCMReaderCallback` (этап [0] — до всего пайплайна).

**Подход:** Новый компонент `SpatialAudioStreamFeeder` создаёт моно streaming AudioClip, читает аудио из LiveKit `AudioStream` через `PCMReaderCallback` и назначает клип на AudioSource.

**Для spatial-источников:**
1. `LivekitAudioSource.New()` создаёт GO с AudioSource + LivekitAudioSource
2. `LivekitAudioSource.Construct()` **НЕ** вызывается → `OnAudioFilterRead` — no-op
3. `SpatialAudioStreamFeeder` добавляется на тот же GO
4. Streaming AudioClip (моно, 1 канал) подаёт данные в AudioSource
5. AudioSource обрабатывает клип штатным пайплайном

**Для non-spatial источников** (loopback): поведение не меняется — `LivekitAudioSource.Construct()` работает как раньше.

### Alternatives Considered

| Вариант | Описание | Причина отказа |
|---------|----------|----------------|
| Ручная спатиализация в OnAudioFilterRead | Pan/rolloff/spread вручную после ReadAudio | Фактически переписывание аудио-движка Unity; не совместимо с Reverb Zones, сторонними spatializer-плагинами |
| Двойной GO с ring buffer | Отдельный GO для захвата (OnAudioFilterRead → ring buffer), отдельный для воспроизведения (AudioClip из ring buffer) | +20мс latency (доп. буфер); двойная буферизация; избыточная сложность |
| Silent clip trick | Назначить пустой AudioClip и надеяться что pan/spatial применится к выходу OnAudioFilterRead | Не решает проблему: panStereo применяется ДО OnAudioFilterRead к данным клипа (тишина) и перезаписывается |
| Модификация LiveKit SDK (Вариант B) | Тот же streaming AudioClip, но инкапсулированный внутри `LivekitAudioSource` через флаг `spatialAudio` | Тот же подход, отличие только организационное (1 компонент вместо 2). Отложен: требует форк SDK, текущий подход решает задачу без изменений SDK |
| Native Spatializer Plugin | Нативный C/C++ плагин для постобработки | Overkill; нужна кросс-платформенная сборка |

---

## Technical Details

### Почему моно (1 канал)

Голосовой сигнал — моно по природе. LiveKit SDK в текущей реализации запрашивает 2 канала и дублирует моно в оба. Для 3D spatialization Unity берёт моно-сигнал и распределяет по каналам на основе позиции источника. Стерео-вход (L=R) — избыточен и некорректен для 3D.

`AudioStream.ReadAudio(data, channels, sampleRate)` поддерживает смену каналов: при первом вызове с `channels=1` пересоздаёт внутренний FFI-стрим на 1 канал (одноразовая операция).

### Потенциальная задержка

`PCMReaderCallback` может вносить задержку порядка одного DSP-буфера (~21мс при 1024 сэмплах / 48kHz). Для voice chat допустимо. При необходимости — уменьшить DSP Buffer Size в Project Settings → Audio.

### Thread Safety

`PCMReaderCallback` вызывается на audio thread — аналогично `OnAudioFilterRead`. `AudioStream.ReadAudio` использует mutex для доступа к буферу — безопасно.

---

## Consequences

### Positive

- panStereo работает корректно (данные в начале пайплайна)
- Audio Effects работают без зависимости от порядка компонентов
- Моно-источник — каноничный формат для Unity 3D audio
- LiveKit SDK не модифицируется
- Минимальные изменения в существующем коде
- Производительность ≈ текущей (меньше данных — моно вместо стерео)

### Negative / Risks

- Дополнительный компонент (`SpatialAudioStreamFeeder`) на GO рядом с `LivekitAudioSource`
- При смене audio device (sample rate) нужно пересоздавать AudioClip
- Одноразовый FFI-вызов при переключении AudioStream с 2 на 1 канал

### Future Considerations

- При форке LiveKit SDK (Вариант B) можно инкапсулировать streaming AudioClip прямо в `LivekitAudioSource` через флаг `spatialMode`, убрав необходимость в отдельном компоненте
- Проверить совместимость с Reverb Zones и сторонними spatializer-плагинами (Steam Audio, Resonance Audio)
