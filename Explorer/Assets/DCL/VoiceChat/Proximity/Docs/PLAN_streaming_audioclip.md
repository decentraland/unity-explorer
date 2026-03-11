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

### Итерация C: Pinna HRTF (C1 + C2) — ЗАВЕРШЕНА

#### C.1: Primary pinna notch

- Peaking EQ biquad (Bristow-Johnson Audio EQ Cookbook) с отрицательным dBGain → notch
- Частота от elevation: `pinnaNotchFreq × (1 + elevationInfluence × normalizedEl × 0.4)` — ±40% сдвиг
- Дефолты: 7000 Hz, Q=4, depth=-9 dB (Lopez-Poveda & Meddis, 1996)
- Biquad state: `hrfZ1L, hrfZ2L, hrfZ1R, hrfZ2R`
- Применяется к обоим ушам (pinna фильтрует обе стороны)
- `enableHRTF = true` → Stage 4 после HeadShadow

#### C.2: Secondary pinna notch

- Частота: `primaryFreq × pinnaSecondaryRatio` (дефолт 1.6× → ~11.2 kHz)
- Глубина: `primaryDepth × pinnaSecondaryStrength` (дефолт 0.6 → -5.4 dB)
- Biquad state: `hrf2Z1L, hrf2Z2L, hrf2Z1R, hrf2Z2R`
- `pinnaSecondaryStrength = 0` → только primary notch (C1 mode)

#### Рефакторинг pipeline

- Разбивка на отдельные проходы (multi-pass) вместо interleaved цикла
- Кэшированный `monoBuffer[]` (allocation-free, resize only on change)
- `ProfilerMarker` для каждой стадии:
  - `LiveKit.Spatial` — обёртка всего pipeline
  - `LiveKit.Spatial.ITD` — delay line
  - `LiveKit.Spatial.ILD` — equal-power gains
  - `LiveKit.Spatial.HeadShadow` — LPF/biquad/multiband
  - `LiveKit.Spatial.HRTF` — peaking EQ notch(es)

#### Inspector параметры HRTF

```csharp
[Header("HRTF — Pinna / Spectral Cues")]
public bool enableHRTF;
[Range(0f, 1f)] public float elevationInfluence = 0.5f;
[Range(4000f, 12000f)] public float pinnaNotchFreq = 7000f;
[Range(1f, 10f)] public float pinnaNotchQ = 4f;
[Range(-20f, 0f)] public float pinnaNotchDepthDb = -9f;

[Header("HRTF — Secondary Notch (C2)")]
[Range(1.2f, 2.5f)] public float pinnaSecondaryRatio = 1.6f;
[Range(0f, 1f)] public float pinnaSecondaryStrength = 0.6f;
```

#### Проверка

- [x] Источник сверху vs снизу — ощущается разница тембра
- [x] `elevationInfluence = 0` → notch на фиксированной частоте
- [x] `pinnaSecondaryStrength = 0` → только primary notch (C1)
- [x] Параметры настраиваются из Inspector в реальном времени
- [x] ProfilerMarkers видны в Unity Profiler
- [x] Переключение enableHRTF → без артефактов

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

Принимается после субъективной оценки итераций C1 и C2. Если двух parametric notch достаточно для voice chat — итерация D пропускается.

---

## Отмеченные, но не планируемые варианты

### SOFA HRTF (полные таблицы)

Полная HRTF свёртка из SOFA-файлов (стандарт AES69). Overkill для voice chat.

---

## Файлы проекта

### SDK (LiveKit fork)

| Файл | Изменения |
|------|-----------|
| `LivekitAudioSource.cs` | `ILDMode`/`ShadowFilterOrder` enum, `enableITD`/`enableHRTF`, multi-pass pipeline, ProfilerMarkers, delay line, cascade/biquad/multiband HeadShadow, peaking EQ notch (C1+C2), `ComputePeakingEQ`, `ApplyBiquad` |

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
