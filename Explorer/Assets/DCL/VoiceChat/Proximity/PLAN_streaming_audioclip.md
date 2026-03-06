# Proximity Voice Chat: Spatial Audio — План

## Контекст

`OnAudioFilterRead` в `LivekitAudioSource` обеспечивает distance rolloff, audio effects и reverb zones. Но **angular panning** (left/right в 3D) не работает из-за стерео формата данных на m_wetGroup — FMOD не панорамирует стерео-источники. См. [ADR](ADR_streaming_audioclip.md).

---

## Что уже работает (без изменений)

- Distance rolloff (затухание с расстоянием) ✓
- Audio Effects на AudioSource (если после LivekitAudioSource в компонентах) ✓
- Reverb Zones ✓
- Amplification/Silence zones (через volume) ✓

---

## Что нужно решить

1. **Angular panning** — направление звука (лево/право) при spatialBlend=1
2. **Camera-relative panning** — пан относительно камеры, не аватара (3rd person)

---

## Варианты для Angular Panning (в обсуждении)

### Вариант 1: Manual Angular Panning в OnAudioFilterRead

**Подход:** Оставить OnAudioFilterRead. После ReadAudio вручную применить L/R balance.

**Шаги:**
1. Кешировать listener position/rotation с main thread каждый кадр (в ECS-системе или Update)
2. В OnAudioFilterRead: рассчитать угол, применить gain к L/R каналам
3. Привязать расчёт к камере (а не AudioListener) для camera-relative panning

**Файлы:**
- Модификация `LivekitAudioSource.cs` (в форке SDK) или новый компонент рядом

**Оценка:** ~1-2 дня

### Вариант 2: Streaming Mono AudioClip + Ring Buffer

**Подход:** Streaming моно AudioClip + промежуточный ring buffer для сглаживания jitter.

**Шаги:**
1. Создать ring buffer для накопления LiveKit аудио
2. PCMReaderCallback читает из ring buffer (а не напрямую из AudioStream)
3. Pre-fill ring buffer для избежания underrun
4. Моно клип → нативный FMOD angular panning

**Файлы:**
- `SpatialAudioStreamFeeder.cs` (расширить ring buffer) или новый компонент
- Возможно модификация LiveKit SDK для прямого доступа к native буферу

**Оценка:** ~3-5 дней (включая тюнинг буфера)

### Вариант 4: Mono Silent Clip + OnAudioFilterRead (эксперимент)

**Подход:** Быстрый тест — назначить моно silent clip, проверить получает ли OnAudioFilterRead channels=1.

**Шаги:**
1. Создать AudioClip.Create("silence", sampleRate, 1, sampleRate, false) — моно, не streaming
2. Назначить на AudioSource, loop=true, Play()
3. В OnAudioFilterRead проверить значение channels
4. Если channels=1 → проверить angular panning

**Оценка:** ~30 минут (эксперимент)

---

## Camera-Relative Panning

Независимо от варианта angular panning:

**При Варианте 1 (Manual Pan):** расчёт угла напрямую относительно `Camera.main.transform` — не зависит от AudioListener.

**При Вариантах 2/4 (нативный FMOD):** AudioListener rotation = camera rotation. Два способа:
- AudioListener на камере (простой, расстояние от камеры)
- AudioListener на аватаре + `rotation = camera.rotation` каждый кадр (точное расстояние)

---

## Рекомендуемый порядок действий

1. **Сначала** — Вариант 4 (эксперимент ~30мин): проверить mono silent clip
2. **Если channels=1 и angular panning работает** → использовать Вариант 4
3. **Если нет** → выбрать между Вариантом 1 (manual pan, быстро, производительно) и Вариантом 2 (нативный FMOD, но latency и сложность)
