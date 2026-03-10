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

`Pan` property + `ProximityPanCalculator` + equal-power cos/sin. Работает, но резкий скачок pan при проходе сзади.

### Итерация 3: Fix ILD + 3D углы — ЗАВЕРШЕНА

`sin(azimuth) * cos(elevation)` вместо clamp. `SetSpatialAngles(azimuth, elevation)`. Плавный переход сзади.

### Итерация A: ITD — ЗАВЕРШЕНА

Delay line (256 сэмплов), формула Woodworth, линейная интерполяция. Режимы ITD (только задержка) и ITD_ILD (задержка + gains).

---

## Итерация B: Рефакторинг → композиция + HeadShadow

**Цель:** Заменить `SpatializationMode` enum на композицию (`ILDMode` + `enableITD` + `enableHRTF`). Реализовать Frequency-Dependent ILD (Head Shadow) — one-pole low-pass на дальнем ухе.

### Шаг B.1: Рефакторинг переключателей

#### SDK (`LivekitAudioSource.cs`)

1. Заменить `SpatializationMode` enum на `ILDMode`:
   ```csharp
   public enum ILDMode { None, EqualPower, HeadShadow }
   ```
2. Заменить поле `spatializationMode` на:
   ```csharp
   [Header("ILD — Interaural Level Difference")]
   public ILDMode ildMode = ILDMode.None;

   [Header("ITD — Interaural Time Difference")]
   public bool enableITD = false;

   [Header("HRTF — Pinna / Spectral Cues")]
   public bool enableHRTF = false;
   ```
3. `New(mono: true)` → `ildMode = ILDMode.EqualPower` (обратная совместимость)
4. Обработка в `OnAudioFilterRead` как pipeline:
   ```
   bool spatialized = (ildMode != None || enableITD || enableHRTF);
   if (!spatialized || channels < 2) return;

   ExtractMono → ApplyITD (optional) → ApplyILD (optional) → ApplyHRTF (optional) → WriteStereo
   ```

#### Unity (`ProximityVoiceChatManager.cs`)

- Без изменений — `New(mono: spatial)` по-прежнему включает ILD EqualPower

### Шаг B.2: HeadShadow (Frequency-Dependent ILD)

#### SDK (`LivekitAudioSource.cs`)

1. Добавить состояние one-pole LPF: `float lpfStateL, lpfStateR`
2. В `ApplyILD`, ветка `HeadShadow`:
   ```
   cutoff = lerp(20000, shadowCutoffHz, |sin(az)| * shadowStrength * cos(el))
   alpha = 1 / (1 + sampleRate / (2 * PI * cutoff))

   Ближнее ухо: обычный gain (без фильтра)
   Дальнее ухо: y[n] = y[n-1] + alpha * (x[n] - y[n-1]), затем gain
   ```
3. Параметры (уже объявлены): `shadowCutoffHz`, `shadowStrength`

### Проверка

- [ ] `ILDMode.None` + `enableITD = false` → стерео passthrough
- [ ] `ILDMode.EqualPower` → прежний pan (регрессии нет)
- [ ] `ILDMode.EqualPower` + `enableITD = true` → ITD + ILD
- [ ] `ILDMode.HeadShadow` → дальнее ухо заметно "глуше"
- [ ] `ILDMode.HeadShadow` + `enableITD` → полная локализация
- [ ] Переключение в Inspector на ходу → без артефактов
- [ ] `shadowCutoffHz` / `shadowStrength` меняют эффект в реальном времени

---

## Итерация C: Pinna HRTF (Spectral Cues)

**Цель:** Вертикальная локализация через notch-фильтры ушной раковины.

### SDK (`LivekitAudioSource.cs`)

1. Добавить biquad notch filter state: `float n1L, n2L, n1R, n2R` (2nd order)
2. Notch frequency зависит от elevation:
   ```
   notchFreq = lerp(6000, 10000, (elevation + PI/2) / PI)   // ~6kHz внизу → ~10kHz вверху
   Q = 3..5   // узкий notch
   depth = elevationInfluence * ...
   ```
3. Biquad коэффициенты пересчитываются при смене угла (раз в буфер, не каждый сэмпл)
4. `enableHRTF = true` → применить notch после ILD

### Проверка

- [ ] Источник сверху vs снизу — ощущается разница тембра
- [ ] Перед vs зад — различается (cone of confusion уменьшается)
- [ ] Параметры настраиваются из Inspector
- [ ] Нет артефактов при резкой смене углов

---

## Файлы проекта

### SDK (LiveKit fork)

| Файл | Изменения |
|------|-----------|
| `LivekitAudioSource.cs` | `ILDMode` enum, `enableITD`, `enableHRTF`, pipeline в OnAudioFilterRead, HeadShadow LPF, (будущее: Pinna notch) |

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
