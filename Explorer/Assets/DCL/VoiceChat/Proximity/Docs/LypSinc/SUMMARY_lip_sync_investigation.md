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
| Текстовый lip sync (PR #7452) | Вне scope | Отдельная фича, не связана с голосом |
| Локальный аватар | Не нужен | Камера не показывает свой рот |
| OVRLipSync | Последняя стадия | Внешняя зависимость, оценить после работы простых подходов |
| Feature flag | Обязателен | Независимый toggle от voice chat |
| Спрайт-атлас | `Mouth_Atlas.png` (1024×1024, 4×4) | Уже создан, совпадает с подходом PR #7452 |

---

## Открытые вопросы

1. **Конфликт с PR #7452 при мерже.** Обе системы пишут `MaterialPropertyBlock` на `Mask_Mouth` рендерер. Нужна приоритизация (голос > текст) или объединение в одну систему.
2. **Assembly-зависимости.** Lip sync система нуждается в доступе к `AvatarShapeComponent` (DCL.AvatarRendering) и `IActiveSpeakers` (LiveKit). Может потребоваться bridge-компонент или общая assembly.
3. **Feature flag.** Создать в `FeaturesRegistry`, определить имя и дефолтное состояние.
4. **Маппинг спрайтов.** Текущая группировка (idle/slight/medium/wide) — первый проход, нужна коррекция после визуального тестирования.
5. **Оживление `Participant.AudioLevel`.** Стоит ли починить мёртвый код на уровне FFI для получения амплитуды без `OnAudioFilterRead`? Потенциально даёт амплитуду без изменений в аудио-пайплайне, но требует работы с Rust FFI.
6. **Лицензия OVRLipSync.** Подтвердить что Meta SDK лицензия разрешает дистрибуцию в non-Oculus билдах Decentraland.

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
