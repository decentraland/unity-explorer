# Proximity Voice Chat: Spatial Audio — План имплементации

## Выбранный подход

**Ручная спатиализация в `LivekitAudioSource.OnAudioFilterRead`** с итеративным развитием алгоритмов. Композиция через независимые переключатели (`ILDMode` enum + `enableITD` bool + `enableHRTF` bool).

Подробный анализ — см. [ADR](ADR_streaming_audioclip.md).

---

## Предусловия

- Локальный форк LiveKit SDK: `c:\DCL\LiveKit\client-sdk-unity`
- Удалённый репозиторий: `https://github.com/decentraland/client-sdk-unity.git`
- Ветка SDK: `feat/mono-spatial-audio` (от `chore/rust-audio-for-mac-intel`)
- Unity manifest (dev): `file:../../../LiveKit/client-sdk-unity`

---

## Завершённые итерации

### Итерация 1: Mono + Zero Pan — ЗАВЕРШЕНА

Моно извлечение из стерео-буфера, дублирование L=R. Без артефактов.

### Итерация 2: Базовый ILD (Equal-Power Pan) — ЗАВЕРШЕНА

`Pan` property + `ProximityPanCalculator` + equal-power cos/sin. Резкий скачок при проходе сзади.

### Итерация 3: Fix ILD + 3D углы — ЗАВЕРШЕНА

`sin(azimuth) * cos(elevation)` вместо clamp. `SetSpatialAngles(azimuth, elevation)`. Плавный переход.

### Итерация A: ITD — ЗАВЕРШЕНА

Delay line (256 сэмплов), формула Woodworth, линейная интерполяция. ITD и ITD+ILD комбинации.

### Итерация B: Рефакторинг + HeadShadow — ЗАВЕРШЕНА

#### B.1: Рефакторинг переключателей

- `SpatializationMode` → `ILDMode` enum (`None`, `EqualPower`, `HeadShadow`)
- `enableITD` bool, `enableHRTF` bool — независимые переключатели
- Pipeline: `mono → ITD → ILD/HeadShadow → HRTF → stereo`
- `New(mono: true)` → `ildMode = EqualPower` (обратная совместимость)

#### B.2: HeadShadow (Frequency-Dependent ILD)

Реализованы 6 режимов фильтра через `ShadowFilterOrder` enum:

| Режим | Спад | Описание |
|-------|------|----------|
| OnePole6dB | 6 dB/oct | Мягкий, subtle |
| TwoPole12dB | 12 dB/oct | **Дефолт**, ближе к реальным ~8-10 dB/oct |
| ThreePole18dB | 18 dB/oct | Усиленный |
| FourPole24dB | 24 dB/oct | Агрессивный |
| Biquad12dB | 12 dB/oct + Q | С настраиваемым резонансом |
| MultiBand3 | 3-band | Per-band gain по измеренной кривой (Blauert, 1997) |

MultiBand3 параметры: `crossoverLowMid=500Hz`, `crossoverMidHigh=2000Hz`, `lowBandDb=-2`, `midBandDb=-10`, `highBandDb=-20`.

Все параметры с [Tooltip] и физическими референсами. Реалистичные дефолты и диапазоны.

---

## Итерация C: Pinna HRTF (Spectral Cues)

**Цель:** Вертикальная локализация. Решение "cone of confusion" — множества точек с одинаковыми ILD/ITD. Ушная раковина (pinna) создаёт elevation-зависимые notch-фильтры в диапазоне 6-10 kHz.

### Шаг C.1: 1 parametric notch

**CPU:** ~1.7 µs / source → ~0.25 ms при 150 источниках (1.2% audio budget)

#### SDK (`LivekitAudioSource.cs`)

1. Добавить biquad notch state: `float hrfZ1L, hrfZ2L, hrfZ1R, hrfZ2R`
2. Notch frequency зависит от elevation:
   ```
   notchFreq = lerp(6000, 10000, (elevation + PI/2) / PI)
   Q = 3..5 (узкий notch)
   depth = elevationInfluence × |sin(elevation)|
   ```
3. Biquad коэффициенты пересчитываются раз в буфер (не каждый сэмпл)
4. `enableHRTF = true` → notch применяется после ILD/HeadShadow

#### Inspector

```csharp
[Header("HRTF — Pinna / Spectral Cues")]
public bool enableHRTF = false;
[Tooltip("Влияние elevation на частоту notch-фильтра (0 = отключено, 1 = полный эффект).")]
[Range(0f, 1f)] public float elevationInfluence = 0.5f;
[Tooltip("Центральная частота notch при elevation=0 (горизонт). 6-8 kHz — типичные значения pinna resonance.")]
[Range(4000f, 12000f)] public float pinnaNotchFreq = 7000f;
[Tooltip("Q-фактор notch filter (узость). 3-5 — типично для pinna.")]
[Range(1f, 10f)] public float pinnaNotchQ = 4f;
[Tooltip("Глубина notch в dB. -6..-12 dB — типично.")]
[Range(-20f, 0f)] public float pinnaNotchDepthDb = -9f;
```

#### Проверка

- [ ] Источник сверху vs снизу — ощущается разница тембра
- [ ] `elevationInfluence = 0` → notch отключен
- [ ] Параметры настраиваются из Inspector в реальном времени
- [ ] Нет артефактов при резкой смене углов

### Шаг C.2: 2 parametric notch

**CPU:** ~3.3 µs / source → ~0.5 ms при 150 источниках (2.4% audio budget)

#### SDK (`LivekitAudioSource.cs`)

1. Добавить вторую пару biquad state: `float hrf2Z1L, hrf2Z2L, hrf2Z1R, hrf2Z2R`
2. Secondary notch на ~1.5-1.6× частоте primary (гармонический overtone pinna):
   ```
   secondaryFreq = primaryNotchFreq × pinnaSecondaryRatio
   secondaryDepth = pinnaNotchDepthDb × 0.5..0.7
   ```
3. Оба notch применяются последовательно

#### Inspector (дополнительно к C.1)

```csharp
[Tooltip("Множитель частоты secondary notch относительно primary. 1.5-1.6× — гармоника pinna.")]
[Range(1.2f, 2.0f)] public float pinnaSecondaryRatio = 1.6f;
[Tooltip("Глубина secondary notch относительно primary (0 = отключен, 1 = та же глубина).")]
[Range(0f, 1f)] public float pinnaSecondaryStrength = 0.6f;
```

#### Проверка

- [ ] Два notch ощущаются как более "объёмная" вертикаль
- [ ] `pinnaSecondaryStrength = 0` → второй notch отключён (регрессия к C.1)
- [ ] Переключение enableHRTF → без артефактов

---

## Итерация D (опциональная): Short FIR 64-tap (HRIR)

**Цель:** Если параметрических notch недостаточно — convolution с реальными импульсными откликами (HRIR).

**CPU:** ~65 µs / source → ~9.8 ms при 150 источниках (~47% audio budget). Допустимо при 50 источниках (~3.3 ms), на грани при 150. Может потребовать LOD: близкие источники — FIR, далёкие — parametric.

### Подготовка данных (pre-baked)

- **Источник:** MIT KEMAR dataset (public domain) — compact HRIR
- **Формат:** Табличка ~25 directions × 64 tap × 2 ears = ~12.5 KB (float16) или ~25 KB (float32)
- **Интерполяция:** Ближайшие 2-3 направления, сферическая интерполяция коэффициентов
- Парсер SOFA не нужен — извлекаем заранее в простой бинарный формат

### Алгоритм

1. По (azimuth, elevation) найти 2-3 ближайших HRIR из таблицы
2. Интерполировать коэффициенты (взвешенно по угловому расстоянию)
3. Convolution: `y[n] = Σ h[k] × x[n-k]` для k=0..63
4. Два отдельных FIR для L и R ушей

### Решение о реализации

Принимается после оценки итераций C1 и C2. Если двух параметрических notch достаточно для задач voice chat (субъективная оценка), итерация D пропускается.

---

## Отмеченные, но не планируемые варианты

### SOFA HRTF (полные таблицы)

Полная HRTF свёртка из SOFA-файлов (стандарт AES69). Даёт максимально точную 3D спатиализацию с индивидуализированными данными.

**Почему не планируется:**
- Требует SOFA-парсер (netCDF / HDF5) — тяжёлая зависимость
- Файлы 5-50 MB на одну HRTF
- Для voice chat overkill — разница с параметрическими notch на речевом сигнале минимальна
- Существующие решения (Steam Audio, Resonance Audio) уже реализуют это как плагин

---

## Файлы проекта

### SDK (LiveKit fork)

| Файл | Изменения |
|------|-----------|
| `LivekitAudioSource.cs` | `ILDMode` enum, `ShadowFilterOrder` enum, `enableITD`, `enableHRTF`, pipeline в OnAudioFilterRead, cascade/biquad/multiband HeadShadow, (будет: Pinna notch, опц. FIR) |

### Unity project

| Файл | Изменения |
|------|-----------|
| `ProximityPanCalculator.cs` | Вычисление azimuth + elevation |
| `ProximityVoiceChatManager.cs` | `New(mono: spatial)` → задать `ildMode` |
| `manifest.json` | `file:` ссылка на локальный SDK (dev) |

---

## Финализация (после всех итераций)

1. Push SDK на GitHub
2. Переключить `manifest.json` на Git URL
3. Оценить перенос `ProximityPanCalculator` в ECS для производительности (50+ участников)
4. Оценить FFI mono оптимизацию (чтение 1 канала из нативного слоя)
