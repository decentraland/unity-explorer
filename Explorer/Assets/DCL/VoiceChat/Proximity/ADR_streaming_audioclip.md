# ADR: Spatial Audio Pipeline для Proximity Voice Chat

**Status:** Accepted (Вариант 6 — Mono в LiveKit SDK + Manual Angular Panning)  
**Date:** 2026-03-06  
**Authors:** Voice Chat team  
**Related:** Итерация 2 из ADR_proximity_voice_chat

---

## Context

В итерации 2 Proximity Voice Chat реализован 3D spatial audio через `LivekitAudioSource` из LiveKit SDK (`com.decentraland.livekit-sdk`, форк `decentraland/client-sdk-unity`). Компонент использует `OnAudioFilterRead` для инъекции аудио в Unity audio pipeline.

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

- `panStereo` → `SoundChannel.cpp:UpdateStereoPan()` → `setPan()` → FMOD Head DSP (PRE-DSP)
- `Volume` → `SoundChannel.cpp:UpdateVolume()` → `setVolume(CachedRolloff × ...)` → POST-DSP
- `CachedRolloff` → `AudioSource.cpp:ApplyDistanceAttenuation()` → `lerp(1, distanceAttenuation, spatialBlend)`
- `3D Position` → `AudioSource.cpp:ApplyPositional()` → `set3DAttributes()` → POST-DSP (FMOD 3D mix matrix)
- `OnAudioFilterRead` → `AudioCustomFilter.cpp:readCallback()` → DSP на m_wetGroup
- Порядок DSP на m_wetGroup → `AudioSource.cpp:GetOrCreateFilterComponents()` → **порядок компонентов на GameObject**

### Почему panStereo не работает

`setPan()` = FMOD Head DSP (этап 3) — ДО m_wetGroup. Применяется к тишине (нет клипа), затем `OnAudioFilterRead` (этап 5) перезаписывает буфер.

### Почему angular panning не работает

FMOD's 3D mix matrix (этап 8) — POST-DSP, но поведение зависит от **формата канала**:
- **Моно** → FMOD распределяет 1 канал по выходам на основе 3D позиции → angular panning
- **Стерео** → FMOD сохраняет стерео-образ, применяет только distance attenuation → нет angular panning

`OnAudioFilterRead` на m_wetGroup **всегда** работает в стерео (channels=2). LiveKit `AudioStream` запрашивает 2 канала у нативного слоя (`currentChannels = 2` хардкод), моно-голос дублируется в L=R. FMOD видит стерео → нет angular panning.

### Почему distance rolloff работает

`setVolume(CachedRolloff × ...)` (этап 7) — POST-DSP, скалярный, не зависит от формата каналов.

### Что работает и не работает с текущим OnAudioFilterRead

| Функция | Работает? | Почему |
|---|---|---|
| Distance rolloff | **Да** | `setVolume` = POST-DSP, скалярный |
| Angular panning (L/R в 3D) | **Нет** | FMOD 3D mix matrix не панорамирует стерео |
| panStereo (2D) | **Нет** | Head DSP к тишине (не нужен для proximity) |
| Audio Effects | **Да** | На m_wetGroup, если после LivekitAudioSource в компонентах |
| Reverb Zones | **Да** | Параллельный SEND |

---

## Рассмотренные варианты

### Вариант 1: Manual Angular Panning (стерео из LiveKit)

Оставить стерео LiveKit. В `OnAudioFilterRead` после `ReadAudio(data, 2, ...)` вручную применить L/R balance по углу.

**+** Нет buffer underrun, синхронный, простой  
**-** Нативный слой LiveKit делает лишнюю моно→стерео конверсию; пишем L=R и потом перевзвешиваем — неэффективно

### Вариант 2: Streaming Mono AudioClip (PCMReaderCallback)

`AudioClip.Create(stream: true, channels: 1)` → нативный FMOD angular panning.

**+** Нативный FMOD panning, все свойства AudioSource работают  
**-** **Buffer underrun** подтверждён тестированием (микро-паузы, рваный звук). Для fix нужен ring buffer → +20-40мс latency

### Вариант 3: Streaming Mono AudioClip в форке SDK

То же что Вариант 2, инкапсулировано в `LivekitAudioSource`.

**+** Чище API  
**-** Те же проблемы buffer underrun + поддержка форка

### Вариант 4: Mono Silent Clip + OnAudioFilterRead

Назначить моно silent clip → FMOD создаёт моно канал → `OnAudioFilterRead` получает channels=1 (гипотеза).

**+** Минимальные изменения  
**-** Не проверено; m_wetGroup вероятно форсит стерео → channels=2 → не сработает

### Вариант 5: Native Spatializer Plugin

Нативный C/C++ плагин.

**+** Максимальный контроль  
**-** Overkill; кросс-платформенная сборка; огромный объём работы

### Вариант 6: Mono в LiveKit SDK + Manual Angular Panning (ВЫБРАН)

В форке LiveKit SDK: `LivekitAudioSource` читает **моно** из нативного слоя и **сам** распределяет по L/R каналам стерео-буфера `OnAudioFilterRead` с angular panning.

**+** Нет buffer underrun (синхронный OnAudioFilterRead)  
**+** Нативный слой не делает лишнюю моно→стерео конверсию  
**+** Буфер LiveKit в 2 раза меньше (моно)  
**+** FFI передаёт в 2 раза меньше данных  
**+** Полный контроль: panning привязывается к камере напрямую  
**+** Distance rolloff, Audio Effects, Reverb Zones работают нативно  
**+** Обратная совместимость: `mono` параметр opt-in, стерео по умолчанию  
**-** Ручная реализация pan law (простая тригонометрия, приемлемо для voice chat)  
**-** Кеширование позиций с main thread для audio thread  
**-** Не совместимо с custom Spatializer Plugins (не нужны для proximity)

---

## Decision

**Вариант 6: Mono в LiveKit SDK fork + Manual Angular Panning.**

### Итерация 1 (текущая): Mono + Zero Pan

Цель — проверить теорию. В форке SDK:
- `LivekitAudioSource.New(mono: true)` читает моно из `AudioStream`
- В `OnAudioFilterRead` дублирует моно в L=R (нулевой pan)
- Никакого angular panning — только проверка что аудио работает чисто без артефактов

### Итерация 2 (следующая): Angular Panning

- Добавить расчёт угла camera → source
- Применить L/R gains в `OnAudioFilterRead`

### Итерация 3 (позже): Camera-Relative Panning

Пан относительно камеры: позиция от аватара, ориентация от камеры.

---

## Camera-Relative Panning

Требование: в 3rd person пан относительно камеры, не аватара. При повороте аватара на 180° при неподвижной камере — звук остаётся "слева в кадре".

**Итерация 1:** Используем позицию камеры для расчёта угла (простой подход).  
**Итерация 2+:** Разделяем — позиция из головы аватара, ориентация из камеры.
