# Lip Sync Investigation Summary

> Хронология и результаты исследования: анимация рта аватаров при голосовом общении через proximity voice chat.

---

## Исходная задача

Аватары в Decentraland proximity voice chat визуально статичны во время речи. Нет визуальной обратной связи, что аватар говорит — никакого движения рта, никакой лицевой анимации. Это критически снижает социальное присутствие и невербальную коммуникацию.

**Цель:** Анимировать спрайты рта аватара в ответ на голосовой чат, выбирая из 16-позного спрайт-атласа (1024×1024, 4×4 сетка из 256px ячеек).

---

## Контекст продакта

Цитата @olavra из PR #7452 (`feat/avatar-blink`):

> "I'm just going to leave this note here so we can remember it later. It seems that this project is well on track and we may have time to experiment with a few things. One of them could be moving the avatars' mouths depending on the phoneme they are reproducing (or at least creating a pseudo-random sequence of sprites to make it look like they are speaking). this is a 1024x1024 sprite atlas, we can fit 16 mouth poses there. Theoretically we can create this texture for all the base mouths and eyes and have different mouth movements. something like this is what I was talking about to recognize phonemes with the voice chat. In this case its a 1-1 character to mouth pose. In the other we will need to recognize frequencies or just randomize the mouth sequence."

> "My idea is to use the same system to have a texture sheet for mouth gestures (phonemes + expressions: sad, smile, surprise, etc) and eyebrows (frown, up, worried etc)."

> "I also think it may be fun communicate this over the network, so clicking over your avatar will force blink, and you can morse code or eye flirt with others... non-verbal communication!"

Ключевые тезисы продакта:
- Это **примитивный 2D facial rig** перед переходом к 3D
- 1024×1024 атлас, 16 поз рта
- Можно распознавать фонемы по голосу, или хотя бы рандомизировать последовательность ртов
- Система расширяема на брови и эмоции
- Идея морзе-кода глазами для невербальной коммуникации

---

## Этап 1: Обзор подходов к анализу речи

Три уровня сложности от простого к сложному:

### 1.1 Amplitude (громкость) — тривиально

Берём PCM-буфер, считаем RMS, по порогам переключаем рот между "закрыт / приоткрыт / открыт".

```csharp
float sum = 0f;
for (int i = 0; i < data.Length; i++) sum += data[i] * data[i];
float rms = Mathf.Sqrt(sum / data.Length);
float amplitude = Mathf.Clamp01(rms * sensitivity);
```

| Плюсы | Минусы |
|-------|--------|
| Тривиально (~5 строк) | Не различает гласные/согласные |
| Ноль аллокаций | "АААА" и "ШШШШ" выглядят одинаково при одной громкости |
| Работает на любом аудио | Рот просто "хлопает" |
| Ничтожная нагрузка (~0.01ms) | Нет информации о фонемах |

### 1.2 FFT Frequency Band Analysis — средняя сложность

Разложение аудио в частотные полосы. Низкие частоты (200–800 Hz) → открытые гласные (А, О). Средние (800–2500 Hz) → закрытые гласные (Е, И). Высокие (2500–8000 Hz) → свистящие (С, Ш, Ф).

| Плюсы | Минусы |
|-------|--------|
| Приблизительно различает гласные/согласные | Требует ручной настройки порогов |
| Нет внешних зависимостей | Результаты плывут между разными голосами |
| Лучше чистой амплитуды | Формантные частоты перекрываются |
| Средняя сложность | Потолок качества ниже OVRLipSync |

CPU: ~0.05–0.1ms на источник (FFT 1024 сэмплов или Goertzel на 3-4 частотах).

### 1.3 Viseme Detection (OVRLipSync) — наилучший результат

Скармливаем PCM-буфер в Meta Oculus Lipsync SDK. Возвращает массив весов 15 визем (Sil, PP, FF, TH, DD, KK, CH, SS, NN, RR, AA, E, I, O, U).

| Плюсы | Минусы |
|-------|--------|
| Лучшее качество — сделано для этой задачи | Внешний нативный плагин |
| Real-time оптимизированный DSP | Один "контекст" на источник (~память) |
| 15 визем → чистый маппинг на 12-16 поз | Лицензия Meta/Oculus SDK |
| Языко-независимый | Нужен пул контекстов для 50+ аватаров |

CPU: ~0.1–0.3ms на `ProcessFrame`.

### 1.4 Сравнительная таблица

| Критерий | Amplitude | FFT Bands | OVRLipSync |
|----------|-----------|-----------|------------|
| Сложность реализации | Часы | Дни | 1–2 дня |
| Точность | Низкая | Средняя | Высокая |
| CPU на источник / кадр | ~0.01ms | ~0.05–0.1ms | ~0.1–0.3ms |
| 50 источников одновременно | Тривиально | Нужен throttling | Нужен пул |
| Внешние зависимости | Нет | Нет | OVRLipSync native |
| Различает A/O/E? | Нет | Грубо | Да |
| Различает гласные/согласные? | Нет | Грубо | Да |
| Потолок качества | "Хлопает" | "Нормально" | "Хорошо-отлично" |

**Рекомендация:** начать с амплитуды (работает за час), потом подключить OVRLipSync для визем. FFT — промежуточный вариант если OVR недоступен.

---

## Этап 2: Анализ PR #7452

PR: https://github.com/decentraland/unity-explorer/pull/7452  
Автор: @olavra  
Статус: Draft  
Ветка: `feat/avatar-blink`

### Что реализовано

**AvatarBlinkSystem** — рандомное моргание глаз:
- Интервал 0.5–5 секунд (настраивается)
- `MaterialPropertyBlock` переключает текстуру глаз на blink-текстуру
- Статический `s_Mpb` переиспользуется для избежания аллокаций
- `SetPropertyBlock(null)` возвращает к дефолтной текстуре

**AvatarMouthAnimationSystem** — **текстовый** lip sync:
- Триггер: `AvatarMouthTalkingComponent` заполняется из `NametagPlacementSystem` при получении chat bubble
- `MapCharToPhoneme` маппит каждый символ текста → индекс визем в Texture2DArray:
  ```
  'a','e','i' → 1;  'b','m','p' → 2;  'f','v' → 3;
  'd' → 4;  'u' → 5;  'c','g','h','k','n','s','t','x','y','z' → 6;
  'o' → 7;  'l' → 8;  'r' → 9;  'ch','j','sh' → 10;  'w','q' → 11;
  default (пробелы, пунктуация) → 0 (idle)
  ```
- Поддержка диграфов (th, ch, sh) через peek на следующий символ
- `PhonemeDuration = 0.1f` (100ms на символ, ~10 fps анимации)
- Визуализация: `MaterialPropertyBlock` + `Texture2DArray` на `Mask_Mouth` рендерере

**Компоненты:**
- `AvatarBlinkComponent` — состояние моргания (таймер, интервал, isBlinking)
- `AvatarMouthAnimationComponent` — состояние рта (текст, индекс символа, таймер, текущий индекс позы)
- `AvatarMouthTalkingComponent` — bridge: текст сообщения + IsDirty флаг

### Ключевой вывод

**PR работает на тексте, не на аудио.** `AvatarMouthAnimationSystem` анимирует рот по chat messages, а не по голосовому чату. Для voice lip sync нужен принципиально другой входной канал — PCM-данные из аудио-потока вместо строки текста.

### Что переиспользуемо из PR

- `FindMouthRenderer` — поиск `Mask_Mouth` в `avatarShape.InstantiatedWearables`
- `MaterialPropertyBlock` паттерн — статический `s_Mpb`, Clear/Set/Apply
- Слайсинг атласа в `Texture2DArray` — `CreateMouthPhonemeTextureArrayAsync`
- Обработка re-instantiation — `MouthRenderer == null` → повторный поиск
- Visibility suppression — `avatarShape.IsVisible` check

---

## Этап 3: Анализ аудио-пайплайна

### Поток аудио от LiveKit до колонок

```
LiveKit Server (WebRTC)
    ↓
FFI → AudioStreamEvent (FrameReceived) → NativeAudioBufferResampleTee
    ↓
LivekitAudioSource.OnAudioFilterRead(float[] data, int channels)
    ↓
AudioStream.ReadAudio(data, channels, sampleRate)   ← чистый моно PCM ЗДЕСЬ
    ↓
[Spatialization Pipeline: ITD → ILD → HeadShadow → HRTF]
    ↓
Unity AudioSource output → колонки
```

**Ключевой файл:** `client-sdk-unity/Runtime/Scripts/Rooms/Streaming/Audio/LivekitAudioSource.cs`

### OnAudioFilterRead — единственная точка доступа к PCM

`OnAudioFilterRead` вызывается на **аудио-потоке Unity** (~46 раз/сек при 48kHz/1024 samples). После `ReadAudio()` буфер содержит чистые моно PCM-данные. Спатиализация (ITD, ILD, HeadShadow, HRTF) применяется после и модифицирует амплитуду per-ear — непригодно для lip sync анализа.

### Потокобезопасность

`OnAudioFilterRead` выполняется на аудио-потоке, ECS-системы — на main thread. Для передачи данных:
- `volatile float` или `Interlocked.Exchange` — для простой амплитуды
- `ConcurrentDictionary<string, float>` — маппинг identity → amplitude
- Lock + array copy — для визем-весов (OVRLipSync)

### Точка врезки для lip sync

Идеальная позиция — **после `ReadAudio`, до проверки spatialization**:

```csharp
// В OnAudioFilterRead:
resource.Value.ReadAudio(data.AsSpan(), channels, sampleRate);

// >>> LIP SYNC: здесь данные чистые, до всех фильтров <<<
// float rms = ComputeRMS(data);
// Interlocked.Exchange(ref _amplitude, rms);

bool spatialized = !bypassSpatialization && (ildMode != ILDMode.None || enableITD || enableHRTF);
if (spatialized && channels >= 2)
    ApplySpatializationPipeline(data, channels);
```

После спатиализации амплитуда зависит от положения слушателя относительно говорящего — некорректно для lip sync (далёкий говорящий будет с закрытым ртом).

---

## Этап 4: Критическая находка — мёртвый код в LiveKit SDK

### Participant.AudioLevel и Participant.Speaking

В `Participant.cs`:
```csharp
public bool Speaking { get; private set; }
public float AudioLevel { get; private set; }
```

**Оба свойства объявлены с `private set` и нигде не устанавливаются** — ни в C# слое, ни из FFI. Это мёртвый код из оригинального LiveKit Unity SDK.

### ActiveSpeakersChanged — только identity, не amplitude

Событие `ActiveSpeakersChanged` несёт только **список identity строк**, не значения громкости:

```csharp
// Room.cs:
case RoomEvent.MessageOneofCase.ActiveSpeakersChanged:
{
    activeSpeakers.UpdateCurrentActives(e.ActiveSpeakersChanged!.ParticipantIdentities!);
}
```

`DefaultActiveSpeakers` — просто `List<string>`:

```csharp
public void UpdateCurrentActives(IEnumerable<string> sids)
{
    actives.Clear();
    actives.AddRange(sids);
    Updated?.Invoke();
}
```

### Вывод

**От LiveKit signaling получаем только бинарный bool "говорит/молчит"**, а не float амплитуду. Для амплитуды нужно либо считать RMS в `OnAudioFilterRead` (~5 строк), либо чинить мёртвый код `Participant.AudioLevel` на уровне FFI (значительно сложнее).

`IActiveSpeakers` обновляется ~4–5 раз/сек через signaling канал с задержкой ~200–500ms.

---

## Этап 5: Перформанс-анализ

### Amplitude (RMS)

- **CPU на источник:** ~0.01ms (одна итерация по 2048 float = ~2K multiply-add)
- **50 источников одновременно:** ~0.5ms суммарно — тривиально
- **Аллокации:** ноль (in-place на существующем буфере)
- **Ограничения:** нет — можно считать для всех источников всегда

### FFT Frequency Bands

- **CPU на источник:** ~0.05–0.1ms (Goertzel на 3-4 частотах или мини-DFT)
- **10 одновременных говорящих:** ~0.5–1.0ms — допустимо
- **50 одновременных:** ~2.5–5ms — нужен throttling (считать каждый 2-й буфер)
- **Аллокации:** нужен scratch buffer, можно переиспользовать

### OVRLipSync

- **CPU на контекст:** ~0.1–0.3ms на `ProcessFrame`
- **Стратегия:** пул из 8 контекстов — выдавать только говорящим аватарам
- **Типичный сценарий:** 2–5 одновременно говорят → 0.5–1.5ms
- **Пул исчерпан:** fallback на amplitude-based animation
- **Память:** каждый контекст ~несколько KB — не проблема даже для 8 шт

### MaterialPropertyBlock

- `SetPropertyBlock` — ~0.01ms на рендерер
- При 5–10 одновременно говорящих — ничтожная нагрузка

### Только говорящие аватары нуждаются в анализе

- Молчащие аватары = idle спрайт, ноль обработки
- В типичном proximity chat одновременно говорят 2–5 из 50+ аватаров
- Distance culling: пропускать аватары дальше ~15м (рот невидим на таком расстоянии)
- Visibility culling: пропускать `IsVisible == false`

---

## Принятые решения

| Решение | Выбор | Обоснование |
|---------|-------|-------------|
| Стартовый источник данных | `IActiveSpeakers` (binary) | Ноль изменений в LiveKit, мгновенный визуальный результат |
| Стартовый алгоритм | Random animation при speaking=true | "Аниме-стиль", мозг дорисовывает соответствие, проверенный приём |
| Итеративная прогрессия | Binary → Amplitude → FFT (optional) → OVR | Каждый шаг independent, можно шипнуть на любом |
| Дефолтный режим | **FrequencyBands** | Лучшее разделение речь/музыка из доступных без внешних зависимостей |
| Текстовый lip sync (PR #7452) | Вне scope | Отдельная фича, не связана с голосом |
| Локальный аватар | Не нужен | Камера не показывает свой рот |
| OVRLipSync | Последняя стадия | Внешняя зависимость, оценить после работы простых подходов |
| Feature flag | Обязателен | Независимый toggle от voice chat |
| Спрайт-атлас | `Mouth_Atlas.png` (1024×1024, 4×4) | Уже создан, совпадает с подходом PR #7452 |
| Отсечка музыки | Spectral peakedness | Ratio dominant_band/total < 0.50 → музыка → idle |

---

## Открытые вопросы

1. **Конфликт с PR #7452 при мерже.** Обе системы пишут `MaterialPropertyBlock` на `Mask_Mouth` рендерер. Нужна приоритизация (голос > текст) или объединение в одну систему.
2. **Feature flag.** Создать в `FeaturesRegistry`, определить имя и дефолтное состояние.
3. **Маппинг спрайтов.** ~~Первый проход, нужна коррекция~~ — **DONE**: скорректировано по визуальному анализу `Mouth_Atlas.png` (Этап 10).
4. **Оживление `Participant.AudioLevel`.** Стоит ли починить мёртвый код на уровне FFI для получения амплитуды без `OnAudioFilterRead`? Потенциально даёт амплитуду без изменений в аудио-пайплайне, но требует работы с Rust FFI.
5. **Лицензия OVRLipSync.** Подтвердить что Meta SDK лицензия разрешает дистрибуцию в non-Oculus билдах Decentraland.
6. **Калибровка spectral peakedness.** Текущий порог 0.50 — эмпирический. Нужно тестирование с разными типами музыки (бас-тяжёлая, инструментальная, вокальная) и разными голосами (мужские, женские, тихие, громкие).
7. **Manifest.json.** Сейчас указывает на локальный путь `file:../../../LiveKit/client-sdk-unity`. Перед merge вернуть на git reference.

---

## Этап 6: Реализация Шага 1 (A2 + P1)

### Что реализовано

Шаг 1 "Random animation при IActiveSpeakers" — полностью реализован и работает. Рты аватаров двигаются при speaking.

### Архитектурное решение: интеграция в ProximityAudioPositionSystem

Вместо отдельной `ProximityLipSyncSystem` — lip sync добавлен **в существующую** `ProximityAudioPositionSystem`:
- **Setup** — ручной метод `SetupPendingLipSync()`, итерирует `activeAudioSources` dict (нужен identity из ключа). Паттерн аналогичен `AssignPendingSources()`.
- **Update** — полноценный `[Query] UpdateLipSync(...)` с `ref ProximityLipSyncComponent` + `ref AvatarShapeComponent`. Source-generated, с `[None(typeof(DeleteEntityIntention))]`.
- **Re-init** — при `MouthRenderer == null` (avatar re-instantiation) повторный поиск через `FindMouthRenderer(in avatarShape)`, аналогично `AvatarBlinkSystem.UpdateBlink` из PR #7452.

### Созданные файлы

| Файл | Назначение |
|------|-----------|
| `VoiceChat/Proximity/ProximityLipSyncComponent.cs` | ECS компонент: `ParticipantIdentity`, `MouthRenderer`, `CurrentPoseIndex`, `PoseHoldTimer` |

### Модифицированные файлы

| Файл | Изменения |
|------|-----------|
| `VoiceChat/Proximity/Systems/ProximityAudioPositionSystem.cs` | Добавлены: `SetupPendingLipSync()`, `[Query] UpdateLipSync(...)`, `FindMouthRenderer()`, shader property IDs, `MaterialPropertyBlock` |
| `VoiceChat/VoiceChatConfiguration.cs` | Секция `[Header("Lip Sync")]`: `MouthAtlasTexture`, `LipSyncPoseHoldDuration`, `LipSyncIdlePoseIndex` |
| `VoiceChat/Proximity/ProximityAudioSettings.cs` | `ProximityConfigHolder` расширен: `MouthTextureArray`, `SpeakingParticipants` (HashSet) |
| `PluginSystem/Global/VoiceChatPlugin.cs` | Atlas slicing (`SliceMouthAtlas`), подписка на `ActiveSpeakers.Updated`, заполнение `SpeakingParticipants` |
| `VoiceChat/Proximity/Mouth_Atlas.png.meta` | `alphaIsTransparency: 1` (match PR #7452) |

### Находка: LiveKit VAD не отпускает speaking state

**Проблема:** Рот продолжает двигаться даже когда участник молчит.

**Причина:** Серверный VAD LiveKit (WebRTC Voice Activity Detection):
- Holdover period ~1–2с после окончания речи
- Чувствительность к фоновому шуму (кулер, дыхание)
- Если микрофон ловит ambient noise → participant остаётся в `ActiveSpeakersChanged.ParticipantIdentities` постоянно
- Это **бинарное** ограничение P1 — именно поэтому в плане Шаг 2 переходит на амплитуду

**Подтверждение что наш код корректен:**
- Identity формат совпадает: Rust FFI `p.identity().to_string()` → C# `ParticipantIdentities`
- Логика idle: `SpeakingParticipants.Contains()` → false → `CurrentPoseIndex = IdlePoseIndex`
- Проблема строго на стороне LiveKit server VAD

### Находка: Threading issue в FFICallback

**Проблема:** `FFICallback` (нативный callback из Rust SDK, `[MonoPInvokeCallback]`) вызывается на **фоновом потоке**. Цепочка: `FFICallback` → `RoomEventReceived` → `Room.OnEventReceived` → `ActiveSpeakers.Updated` → наш handler (пишет в `HashSet<string>`). ECS система читает `HashSet` с main thread.

**Риск:** Data race на `HashSet<string>` (не thread-safe). На практике не вызывает видимых проблем (малый набор данных, быстрые операции), но формально UB.

**Решение для Шага 2:** Перейти на `ConcurrentDictionary` или `volatile` / `Interlocked` паттерн при добавлении amplitude-based detection.

---

## Этап 7: Реализация Шага 2 (A4 + P2) — Amplitude + Weighted Random

### Что реализовано

Шаг 2 полностью заменяет бинарный `IActiveSpeakers` на amplitude-based detection. Алгоритм — **amplitude + weighted random** (A4 из ADR):
1. RMS amplitude сглаживается через exponential Lerp
2. Smoothed amplitude определяет **группу** поз (SLIGHT / MEDIUM / WIDE)
3. `Random.Range` выбирает **конкретную позу** внутри группы
4. При тишине (`< SilenceThreshold`) — idle

**Результат тестирования:** "Это хорошо работает" — рот реагирует на громкость, при тишине — idle, визуально убедительно.

### Изменения в LiveKit SDK

**`LivekitAudioSource.cs`:**
- Добавлено private поле `float lipSyncAmplitude` + public property `LipSyncAmplitude` через `Interlocked.CompareExchange` (thread-safe read)
- В `OnAudioFilterRead()` **перед** спатиализацией (`spatialDsp.Process`): RMS вычисление + `Interlocked.Exchange` для записи
- Pre-spatialization = амплитуда не зависит от позиции слушателя

**`manifest.json`:**
- LiveKit SDK переключен на локальный путь `file:../../../LiveKit/client-sdk-unity` для быстрой итерации без push в remote

### Изменения в Unity Explorer

**`ProximityLipSyncComponent.cs`:**
- Добавлены `LivekitAudioSource LivekitSource` (кэшированная ссылка) и `float SmoothedAmplitude`

**`VoiceChatConfiguration.cs`:**
- Новая секция `[Header("Lip Sync — Amplitude")]`:
  - `LipSyncAmplitudeSensitivity` (default 8) — множитель raw RMS
  - `LipSyncSmoothingFactor` (default 0.3) — скорость сглаживания
  - `LipSyncSilenceThreshold` (default 0.01) — порог тишины

**`ProximityAudioPositionSystem.cs`:**
- Добавлены static массивы pose groups: `POSES_SLIGHT`, `POSES_MEDIUM`, `POSES_WIDE`
- `SetupPendingLipSync()` — добавлено кэширование `LivekitAudioSource` через `GetComponent` (одноразовый вызов)
- `UpdateLipSync` query переписан: читает `LivekitSource.LipSyncAmplitude`, сглаживает, выбирает pose group по smoothed amplitude

### Threading issue — решён архитектурно

Шаг 1 использовал `HashSet<string> SpeakingParticipants` (запись из native thread, чтение из main thread = data race).  
Шаг 2 **обходит проблему**: amplitude читается через `Interlocked` прямо из `LivekitAudioSource`, без промежуточных коллекций. Каждый `LivekitAudioSource` — свой атомарный float, никаких shared mutable collections.

---

## Этап 8: Speech Band Filter (Step 2.5)

### Проблема

При включённой фоновой музыке рот аватара реагирует на музыку, а не только на речь. Full-spectrum RMS не различает голос от других звуковых источников.

### Решение

Добавлен bandpass filter (high-pass + low-pass, one-pole IIR) в `LivekitAudioSource.OnAudioFilterRead`. Оба RMS вычисляются параллельно в одном проходе:
- **Full-spectrum** — как раньше, все частоты
- **Speech-band** — только channel 0, после bandpass filter

Дизайнер переключает через `LipSyncUseSpeechBandFilter` bool и подстраивает `SpeechBandLowHz` / `SpeechBandHighHz`.

### Файлы

| Файл | Изменения |
|------|-----------|
| `LivekitAudioSource.cs` | `ComputeLipSyncAmplitudes()` — bandpass filter + оба RMS; filter state fields; `speechBandLowHz`/`speechBandHighHz` internal fields; `LipSyncSpeechAmplitude` property |
| `VoiceChatConfiguration.cs` | `LipSyncUseSpeechBandFilter`, `LipSyncSpeechBandLowHz` (300), `LipSyncSpeechBandHighHz` (3000) |
| `ProximityAudioPositionSystem.cs` | Sync band params → LivekitAudioSource per frame; pick amplitude by filter toggle |

### Технические детали

- **High-pass**: `y[n] = α * (y[n-1] + x[n] - x[n-1])`, α = RC/(RC+dt), removes below `speechBandLowHz`
- **Low-pass**: `y[n] = y[n-1] + α * (x[n] - y[n-1])`, α = dt/(RC+dt), removes above `speechBandHighHz`
- Mono processing (channel 0 only, `i % channels == 0`) — корректный filter state без интерливинга
- Filter state persistent across `OnAudioFilterRead` calls (полные `hpState`, `hpPrevInput`, `lpState`)
- Band params синхронизируются из `VoiceChatConfiguration` → `LivekitAudioSource` каждый кадр в `UpdateLipSync` query

---

## Этап 9: Реализация Шага 3 (A5 + P3) — Frequency Bands

### Проблема из Шага 2.5

One-pole bandpass filter "так себе отработал" — даже при сужении до 1000-1000 Hz. One-pole слишком пологий (6 dB/octave), не отфильтровывает музыку в том же диапазоне что и речь.

### Решение: Goertzel + LipSyncMode enum

Добавлены 3 режима lip sync через `LipSyncMode` enum:

| Режим | Источник данных | Pose selection |
|-------|----------------|----------------|
| `AmplitudeWeighted` | Full-spectrum RMS | По громкости: SLIGHT/MEDIUM/WIDE |
| `SpeechBandAmplitude` | Bandpass-filtered RMS | По громкости: SLIGHT/MEDIUM/WIDE |
| `FrequencyBands` | Goertzel band energies | По типу звука: vowel/consonant/sibilant |

**Goertzel algorithm** вычисляет энергию на 3 представительных частотах:
- 500 Hz (Low) — открытые гласные А, О
- 1500 Hz (Mid) — закрытые гласные Е, И
- 4000 Hz (High) — свистящие С, Ш, Ф

Всё считается параллельно в одном `ComputeLipSyncAmplitudes()` — ~6 multiply-add на mono-сэмпл сверх existing.

### Файлы

| Файл | Изменения |
|------|-----------|
| `LivekitAudioSource.cs` | Goertzel accumulators, `LipSyncBandLow/Mid/High` properties, нормализация `invN²` |
| `VoiceChatConfiguration.cs` | `LipSyncMode` enum, `LipSyncBandSensitivity`, убран `LipSyncUseSpeechBandFilter` bool |
| `ProximityAudioPositionSystem.cs` | `SelectPoseByBands()`, `SelectPoseByAmplitude()`, pose groups `OPEN_VOWEL/CLOSED_VOWEL/SIBILANT`, mode switch в query |

---

## Этап 10: Bug Fixes и оптимальные дефолты для Voice Chat

### Найденные проблемы

После реализации Шага 3 (FrequencyBands) обнаружено, что **разница между режимами практически не видна** и **фоновая музыка по-прежнему распознаётся как речь**. Ревизия кода выявила 5 проблем:

#### 1. Silence gate слишком слабый

`SilenceThreshold = 0.01` при `AmplitudeSensitivity = 8` означал, что raw RMS > 0.00125 открывает gate. Это буквально любой звук, включая тихую фоновую музыку.

#### 2. FrequencyBands не отличал музыку от речи

Bands **только выбирали тип позы** (sibilant/vowel), но никогда не возвращали idle. Если gate открыт — рот **всегда** двигался, даже на музыке. `SelectPoseByBands` не имел механизма отсечки.

#### 3. Отсутствовал spectral peakedness check

Ключевое отличие речи от музыки: **речь концентрирует энергию в 1-2 полосах** (ratio dominant/total ~0.6-0.9), **музыка распределяет равномерно** (ratio ~0.33). Проверка отсутствовала.

#### 4. Goertzel формула содержала лишнее +0.5 смещение

```csharp
// Было (лишнее +0.5):
float gCoeff = 2f * Mathf.Cos(2f * Mathf.PI * (0.5f + monoLen * freq / sampleRate) / monoLen);
// Стало (стандартная формула):
float gCoeff = 2f * Mathf.Cos(2f * Mathf.PI * freq / sampleRate);
```

На практике влияние минимальное (~0.35 градуса фазы при N=512), но формула была некорректной.

#### 5. Дефолтный режим был AmplitudeWeighted

Наименее продвинутый режим без какой-либо фильтрации музыки стоял по умолчанию.

### Исправления

**Spectral peakedness check в `SelectPoseByBands`:**

```csharp
float maxBand = Mathf.Max(low, Mathf.Max(mid, high));
float total = low + mid + high;
if (maxBand / total < peakednessThreshold) return idlePose; // music → idle
```

Теперь `SelectPoseByBands` может вернуть `idlePose` если спектральная форма сигнала больше похожа на музыку, чем на речь.

**Новый параметр `LipSyncSpectralPeakedness`:**

```csharp
[Range(0.35f, 0.7f)]
public float LipSyncSpectralPeakedness = 0.50f;
```

Настраиваемый порог отсечки: ниже = более терпим к музыке, выше = строже. С 3 полосами: идеально равномерно = 0.333, речь = 0.5-0.9.

### Оптимальные дефолты для Voice Chat

| Параметр | Было | Стало | Почему |
|----------|------|-------|--------|
| `LipSyncMode` | AmplitudeWeighted | **FrequencyBands** | Лучшее разделение речь/музыка |
| `LipSyncAmplitudeSensitivity` | 8 | **5** | 8 слишком агрессивно усиливал шум |
| `LipSyncSmoothingFactor` | 0.3 | **0.25** | Чуть плавнее, меньше дёрганий |
| `LipSyncSilenceThreshold` | 0.01 | **0.06** | 0.01 пропускал любой звук, 0.06 фильтрует тихий фон |
| `SilenceThreshold Range` | 0–0.15 | **0–0.30** | Расширен для возможности агрессивной отсечки |
| `LipSyncPoseHoldDuration` | 0.1 | **0.08** | Чуть отзывчивее |
| `LipSyncSpeechBandLowHz` | 300 | **200** | Захватывает мужские голоса (F0 ~85 Hz, F1 ~300 Hz) |
| `LipSyncSpeechBandHighHz` | 3000 | **3500** | Захватывает сибилянты лучше |
| `LipSyncSpectralPeakedness` | (нет) | **0.50** | Ключевой параметр отсечки музыки |

### Обновлённые pose groups (визуальный анализ Mouth_Atlas.png)

Группы скорректированы по результатам визуального анализа 16 поз атласа:

**Amplitude-weighted (SLIGHT / MEDIUM / WIDE):**

```
SLIGHT (barely open):  5=small oo, 7=small o, 11=tiny oval, 15=narrow smirk
MEDIUM (mid-open):     1=round O, 3=teeth+tongue, 8=oval+tongue, 9=oval+tongue, 14=round O
WIDE (wide open):      0=teeth grimace, 4=AH+tongue, 6=smile+tongue, 10=teeth grid, 12=grin, 13=big open
```

**Frequency-band (OPEN_VOWEL / CLOSED_VOWEL / SIBILANT):**

```
OPEN_VOWEL (wide, tongue visible — A, O):    4, 6, 8, 9, 13
CLOSED_VOWEL (rounded/small — O, OO, E):     1, 5, 7, 11, 14
SIBILANT (teeth visible, narrow — S, SH, F): 0, 3, 10, 12, 15
```

### Рецепты для дизайнера

**"Музыка играет, рот двигается":**
1. `SpectralPeakedness` → 0.55–0.60
2. `SilenceThreshold` → 0.08–0.10
3. Убедиться `LipSyncMode = FrequencyBands`

**"Рот не реагирует на тихую речь":**
1. `AmplitudeSensitivity` → 8–10
2. `SilenceThreshold` → 0.03–0.04
3. `SpectralPeakedness` → 0.40–0.45

**"Рот мельтешит/дёргается":**
1. `SmoothingFactor` → 0.15–0.2
2. `PoseHoldDuration` → 0.12–0.15

**"Рот вяло реагирует, запаздывает":**
1. `SmoothingFactor` → 0.4–0.5
2. `PoseHoldDuration` → 0.06–0.08

### Файлы (фактические)

| Действие | Файл |
|----------|------|
| **Modified** | `LivekitAudioSource.cs` — Goertzel coefficient fix (removed +0.5 offset) |
| **Modified** | `ProximityAudioPositionSystem.cs` — spectral peakedness check в `SelectPoseByBands`, updated pose groups, `idlePose` passthrough |
| **Modified** | `VoiceChatConfiguration.cs` — `LipSyncSpectralPeakedness` param, updated all defaults, extended `SilenceThreshold` Range |

---

## Ключевые файлы

### Unity Explorer (`unity-explorer`)

| Файл | Роль |
|------|------|
| `Explorer/Assets/DCL/VoiceChat/Proximity/ProximityVoiceChatManager.cs` | Менеджер proximity voice chat, создаёт LivekitAudioSource per participant |
| `Explorer/Assets/DCL/VoiceChat/Proximity/Systems/ProximityAudioPositionSystem.cs` | ECS: маппинг identity → entity, позиция AudioSource = HeadAnchorPoint |
| `Explorer/Assets/DCL/VoiceChat/Proximity/ProximityAudioSourceComponent.cs` | ECS компонент: AudioSource + Transform |
| `Explorer/Assets/DCL/VoiceChat/Proximity/Mouth_Atlas.png` | Спрайт-атлас 16 поз рта (1024×1024, 4×4) |
| `Explorer/Assets/DCL/VoiceChat/VoiceChatParticipantsStateService.cs` | Сервис состояния участников, подписка на ActiveSpeakers.Updated |
| `Explorer/Assets/DCL/VoiceChat/VoiceChatConfiguration.cs` | Конфигурация voice chat (proximity spatial settings) |
| `Explorer/Assets/DCL/Multiplayer/Profiles/Tables/EntityParticipantTable.cs` | walletId → Entity маппинг |
| `Explorer/Assets/DCL/AvatarRendering/AvatarShape/Systems/AvatarMouthAnimationSystem.cs` | **PR #7452**: текстовый lip sync |
| `Explorer/Assets/DCL/AvatarRendering/AvatarShape/Systems/AvatarBlinkSystem.cs` | **PR #7452**: моргание глаз |
| `Explorer/Assets/DCL/AvatarRendering/AvatarShape/Components/AvatarMouthAnimationComponent.cs` | **PR #7452**: компонент анимации рта |
| `Explorer/Assets/DCL/AvatarRendering/AvatarShape/Components/AvatarMouthTalkingComponent.cs` | **PR #7452**: bridge от chat к mouth |
| `Explorer/Assets/DCL/AvatarRendering/AvatarShape/Components/AvatarBlinkComponent.cs` | **PR #7452**: компонент моргания |
| `Explorer/Assets/DCL/AvatarRendering/AvatarShape/Rendering/TextureArray/TextureArrayConstants.cs` | Shader property IDs для текстур-массивов |
| `Explorer/Assets/DCL/AvatarRendering/Loading/Assets/CachedAttachment.cs` | Wearable attachment с рендерерами |
| `Explorer/Assets/DCL/PluginSystem/Global/AvatarPlugin.cs` | Plugin: инициализация avatar систем, слайсинг атласа |

### LiveKit Client SDK (`client-sdk-unity`)

| Файл | Роль |
|------|------|
| `Runtime/Scripts/Rooms/Streaming/Audio/LivekitAudioSource.cs` | Основной источник: OnAudioFilterRead, spatialization pipeline |
| `Runtime/Scripts/Rooms/Participants/Participant.cs` | Speaking (dead), AudioLevel (dead), Identity, Tracks |
| `Runtime/Scripts/Rooms/ActiveSpeakers/DefaultActiveSpeakers.cs` | `List<string>` активных говорящих, event Updated |
| `Runtime/Scripts/Rooms/ActiveSpeakers/IActiveSpeakers.cs` | Интерфейс: `IReadOnlyCollection<string>` + event |
| `Runtime/Scripts/Rooms/Room.cs` | Обработка RoomEvents, ActiveSpeakersChanged dispatch |
| `Runtime/Scripts/Rooms/IRoom.cs` | IRoom интерфейс: ActiveSpeakers, Participants, DataPipe |
