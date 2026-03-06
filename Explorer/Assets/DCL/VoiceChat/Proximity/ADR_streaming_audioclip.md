# ADR: Spatial Audio Pipeline для Proximity Voice Chat

**Status:** In Discussion (angular panning approach)  
**Date:** 2026-03-06  
**Authors:** Voice Chat team  
**Related:** Итерация 2 из ADR_proximity_voice_chat

---

## Context

В итерации 2 Proximity Voice Chat реализован 3D spatial audio через `LivekitAudioSource` из LiveKit SDK. Компонент использует `OnAudioFilterRead` для инъекции аудио в Unity audio pipeline.

**Обнаруженные проблемы:**
1. `panStereo` (2D pan) не работает — звук одинаков в обоих каналах при любом значении pan
2. **Angular panning (left/right в 3D)** не работает — при `spatialBlend = 1` звук не смещается влево/вправо при перемещении источника вокруг слушателя
3. Distance rolloff (затухание с расстоянием) — **работает** корректно

---

## Analysis

### Точный DSP-пайплайн Unity (из исходного кода)

На основе анализа `AudioSource.cpp`, `SoundChannel.cpp`, `AudioCustomFilter.cpp` из исходного кода Unity (FMOD-based):

```
1.  AudioClip PCM / PCMReaderCallback
         ↓
2.  Pitch (FMOD Channel: setFrequency = pitch × dopplerPitch × baseFreq)
         ↓
3.  FMOD Head DSP: panStereo (setPan → matrix DSP в начале цепочки)     ← PRE-DSP
         ↓
    ═══ m_dryGroup ═══
4.  [Опц.] Spatializer Plugin (pre-effects, если spatializePostEffects=false)
         ↓
    ═══ m_wetGroup ═══
5.  Built-in Effects + OnAudioFilterRead                                  ← в порядке компонентов
         ↓
6.  [Опц.] Spatializer Plugin (post-effects, если spatializePostEffects=true)
         ↓
    ═══ Channel Output Mixing (FMOD) ═══
7.  Volume (setVolume = CachedRolloff × channelVol × ambientVol)          ← POST-DSP
8.  3D Angular Panning (set3DPanLevel × set3DAttributes × set3DSpread)    ← POST-DSP
         ↓
9.  Parent Group (AudioMixer / AudioManager + Listener Effects)
         +
    Reverb Zones (параллельный SEND, не insert)
```

**Источники в коде Unity:**

- `panStereo` → `SoundChannel.cpp:UpdateStereoPan()` → `setPan()` → FMOD вставляет matrix DSP в HEAD цепочки (PRE-DSP)
- `Volume` → `SoundChannel.cpp:UpdateVolume()` → `setVolume(CachedRolloff × ...)` → POST-DSP (output mixing)
- `CachedRolloff` → `AudioSource.cpp:ApplyDistanceAttenuation()` → `lerp(1, distanceAttenuation, spatialBlend)`
- `3D Position` → `AudioSource.cpp:ApplyPositional()` → `set3DAttributes()` → POST-DSP (FMOD 3D mix matrix)
- `OnAudioFilterRead` → `AudioCustomFilter.cpp:readCallback()` → DSP на m_wetGroup
- Порядок DSP на m_wetGroup → `AudioSource.cpp:GetOrCreateFilterComponents()` → итерация `GetComponentAtIndex(i)` → **порядок компонентов на GameObject**

### Почему panStereo не работает

`setPan()` реализован как FMOD Head DSP (этап 3) — ДО m_wetGroup. Применяется к тишине (нет клипа), затем `OnAudioFilterRead` (этап 5) перезаписывает буфер данными LiveKit.

### Почему angular panning не работает

FMOD's 3D mix matrix (этап 8) — POST-DSP, применяется к данным из m_wetGroup. Однако **формат канала** определяет поведение:
- **Моно источник** → FMOD берёт 1 канал и распределяет по выходным каналам на основе 3D позиции → angular panning работает
- **Стерео источник** → FMOD сохраняет стерео-образ и применяет только distance attenuation → angular panning **не работает**

`OnAudioFilterRead` на m_wetGroup **всегда** работает в стерео (channels=2), потому что m_wetGroup процессит в формате системного выхода. LiveKit пишет одинаковые данные в L и R (моно в стерео обёртке). FMOD видит стерео → не делает angular panning.

### Почему distance rolloff работает

`setVolume(CachedRolloff × ...)` (этап 7) — POST-DSP, не зависит от формата каналов. Скалярное умножение громкости применяется к любому формату.

### Что работает и не работает с текущим OnAudioFilterRead

| Функция | Работает? | Почему |
|---|---|---|
| Distance rolloff | **Да** ✓ | `setVolume` = POST-DSP, скалярный, не зависит от формата |
| Angular panning (L/R в 3D) | **Нет** ✗ | FMOD 3D mix matrix не панорамирует стерео-источники |
| panStereo (2D) | **Нет** ✗ | Head DSP, PRE-DSP → применяется к тишине |
| Audio Effects | **Да** ✓ | На m_wetGroup, если компонент после LivekitAudioSource |
| Reverb Zones | **Да** ✓ | Параллельный SEND, не insert |
| Amplification/Silence zones | **Да** ✓ | Через volume = POST-DSP |

---

## Problem Statement

Для proximity voice chat нужно angular panning — игрок должен слышать, с какой стороны идёт голос другого игрока. Текущий `OnAudioFilterRead` не обеспечивает angular panning из-за стерео формата канала.

Дополнительное требование: **пан должен быть относительно камеры**, а не головы аватара. В 3rd person при повороте аватара на 180° при неподвижной камере звук должен оставаться "слева в кадре", а не переходить в правый канал. Решается через AudioListener (rotation = camera rotation).

---

## Варианты решения Angular Panning

### Вариант 1: Manual Angular Panning в OnAudioFilterRead

**Суть:** Оставить `OnAudioFilterRead` для доставки аудио. После `ReadAudio` вручную рассчитать угол между источником и слушателем и применить L/R balance к стерео данным.

```
OnAudioFilterRead:
    1. ReadAudio(data, 2, sampleRate)  — LiveKit пишет стерео (L=R)
    2. angle = SignedAngle(listener.forward, sourcePos - listenerPos, listener.up)
    3. leftGain = cos((angle + 90) × π / 360)
    4. rightGain = sin((angle + 90) × π / 360)
    5. data[i*2] *= leftGain; data[i*2+1] *= rightGain
```

**Плюсы:**
- Нет buffer underrun — OnAudioFilterRead синхронен с DSP output
- Максимальная производительность (простая тригонометрия per-sample)
- Не требует модификации LiveKit SDK (можно сделать в ProximityVoiceChatManager или отдельном компоненте)
- Distance rolloff, Audio Effects, Reverb Zones продолжают работать нативно
- Полный контроль: можно привязать к камере а не к AudioListener
- Можно реализовать spread (ширина источника) через blend коэффициентов

**Минусы:**
- Ручная реализация pan law — не точно соответствует нативному FMOD 3D panning
- Потоковая безопасность: позиции listener/source нужно кешировать с main thread (OnAudioFilterRead на audio thread)
- Не работает с custom Spatializer Plugins (Steam Audio, Resonance Audio)
- panStereo (2D) по-прежнему не работает (не нужен для proximity)
- Нужно самостоятельно поддерживать HRTF если понадобится

### Вариант 2: Streaming Mono AudioClip (через PCMReaderCallback)

**Суть:** Подавать аудио через `AudioClip.Create(stream: true, channels: 1)`. FMOD создаёт моно канал → нативный 3D angular panning работает.

**Плюсы:**
- Нативное 3D angular panning от FMOD — точное, проверенное
- panStereo работает (данные в начале пайплайна)
- Audio Effects работают без зависимости от порядка компонентов
- Моно-источник — каноничный формат для 3D audio
- Совместимо с custom Spatializer Plugins

**Минусы:**
- **Buffer underrun** — PCMReaderCallback вызывается асинхронно относительно LiveKit буфера; при нехватке данных → нули → микро-паузы, рваный звук (подтверждено тестированием)
- Для fix buffer underrun нужен промежуточный ring buffer → +20-40мс latency
- Streaming AudioClip менее предсказуем по таймингу чем OnAudioFilterRead
- При смене audio device нужно пересоздавать AudioClip

### Вариант 3: Streaming Mono AudioClip в форке LiveKit SDK

**Суть:** То же что Вариант 2, но инкапсулировано внутри `LivekitAudioSource` через флаг `spatialAudio`.

**Плюсы:** Те же что Вариант 2 + один компонент вместо двух, чище API  
**Минусы:** Те же что Вариант 2 + требует поддержку форка SDK

### Вариант 4: Mono Silent Clip + OnAudioFilterRead

**Суть:** Создать короткий бесшумный моно AudioClip, назначить на AudioSource. FMOD создаёт моно канал. `OnAudioFilterRead` получает `channels=1` (гипотеза) → пишем моно данные → FMOD делает нативный 3D angular panning.

**Плюсы:**
- Синхронная доставка через OnAudioFilterRead (нет buffer underrun)
- Нативный 3D angular panning (если FMOD действительно обработает моно)
- Минимальные изменения (только создать silent clip)

**Минусы:**
- **Не проверено** — неизвестно, получает ли OnAudioFilterRead channels=1 при моно клипе или m_wetGroup всегда форсит стерео
- Если m_wetGroup процессит в стерео (вероятно), то FMOD всё равно увидит стерео на выходе → angular panning не сработает
- panStereo по-прежнему не работает (Head DSP к тишине)

### Вариант 5: Native Spatializer Plugin

**Суть:** Написать нативный C/C++ Spatializer Plugin который обрабатывает данные после OnAudioFilterRead.

**Плюсы:** Полный контроль на нативном уровне, максимальная производительность  
**Минусы:** Overkill; нужна кросс-платформенная сборка; огромный объём работы

### Сводная таблица

| | Вар.1: Manual Pan | Вар.2: Streaming Clip | Вар.3: SDK Fork | Вар.4: Silent Clip | Вар.5: Native Plugin |
|---|---|---|---|---|---|
| Angular panning | Ручной (simple pan law) | Нативный FMOD | Нативный FMOD | Нативный FMOD (если работает) | Нативный |
| Buffer underrun | **Нет** | **Да** (проблема) | **Да** | **Нет** | **Нет** |
| Latency | 0 | +20-40мс (с ring buffer fix) | +20-40мс | 0 | 0 |
| Сложность | Низкая | Средняя | Средняя | Минимальная (но непроверено) | Очень высокая |
| Модификация SDK | Нет | Нет | Да | Нет | Нет |
| Совместимость с Spatializer Plugins | Нет | Да | Да | Да (если работает) | Да |
| Производительность | Высокая | Средняя | Средняя | Высокая | Максимальная |

---

## Camera-Relative Panning

Независимо от выбранного варианта, angular panning должен быть относительно камеры:

**Вариант A: AudioListener на камере** — стандартный подход Unity. Пан относительно камеры автоматически. Расстояние считается от камеры (не аватара) — отличие ~5-10м в 3rd person при min/max distance 2-50м, приемлемо.

**Вариант B: AudioListener на аватаре, rotation = camera.rotation** — точное расстояние от аватара, пан относительно камеры. Нужен скрипт синхронизации rotation каждый кадр.

При Варианте 1 (Manual Pan) можно привязать расчёт угла напрямую к камере, без привязки к AudioListener.

---

## Decision

**В обсуждении.** Рекомендуемые кандидаты: Вариант 1 (Manual Pan) или Вариант 4 (Silent Mono Clip — требует проверки).
