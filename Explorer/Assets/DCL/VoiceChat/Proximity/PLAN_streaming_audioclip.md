# Proximity Voice Chat: Spatial Audio — План имплементации

## Выбранный подход

**Ручная спатиализация в `LivekitAudioSource.OnAudioFilterRead`** с итеративным развитием алгоритмов.

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

---

## Итерация 3: Fix ILD + 3D углы

**Цель:** Плавный pan при проходе сзади, поддержка elevation (3D).

### SDK (`LivekitAudioSource.cs`)

1. Добавить enum `SpatializationMode` (None, ILD, ITD, ITD_ILD, ParametricHRTF)
2. Заменить `Pan` property на `SetSpatialAngles(float azimuth, float elevation)` — два float, set main thread, read audio thread
3. Заменить `monoMode` + pan → `spatializationMode != None` означает mono mode
4. Добавить Inspector-параметры с `[Header]`:
   ```
   [Header("Spatialization")]
   SpatializationMode spatializationMode

   [Header("ILD — Interaural Level Difference")]
   float ildStrength = 1.0          // 0..1

   [Header("ITD — Interaural Time Difference")]
   float headRadius = 0.0875        // метры, 0.05..0.15

   [Header("Parametric HRTF — Head Shadow")]
   float shadowCutoffHz = 1500      // 500..4000
   float shadowStrength = 0.7       // 0..1
   float elevationInfluence = 0.5   // 0..1
   ```
5. В `OnAudioFilterRead`, ILD ветка:
   ```
   pan = sin(azimuth) * cos(elevation) * ildStrength
   gainL = cos((pan+1)*0.5 * PI/2)
   gainR = sin((pan+1)*0.5 * PI/2)
   ```

### Unity (`ProximityPanCalculator.cs`)

1. Вычислять azimuth и elevation (радианы) вместо pan:
   ```
   local = listenerTransform.InverseTransformDirection(direction)
   azimuth = Atan2(local.x, local.z)                        // -PI..+PI
   horizontalDist = sqrt(local.x² + local.z²)
   elevation = Atan2(local.y, horizontalDist)                // -PI/2..+PI/2
   livekitAudioSource.SetSpatialAngles(azimuth, elevation)
   ```
2. Убрать spatialBlend из расчёта (SDK решает сам)

### Проверка

- [ ] Плавный переход pan при проходе источника сзади (нет скачка)
- [ ] Источник точно сверху/снизу → звук по центру (elevation)
- [ ] Enum переключается в Inspector → меняется алгоритм в реальном времени
- [ ] `SpatializationMode.None` → стерео passthrough (обратная совместимость)

---

## Итерация A: + ITD (Interaural Time Difference)

**Цель:** Задержка звука в дальнем ухе. Улучшает пространственное восприятие на низких частотах.

### SDK (`LivekitAudioSource.cs`)

1. Добавить кольцевой буфер (delay line) — `float[] delayBuffer`, `int delayWritePos`
   - Размер: `(int)(headRadius / 343f * sampleRate) * 2 + 2` — максимальная задержка с запасом
   - Аллоцируется один раз при `OnEnable` или при смене sampleRate
2. В `OnAudioFilterRead`, ITD ветка:
   ```
   delaySamples = headRadius * (azimuth + sin(azimuth)) / (2 * 343) * sampleRate
   Ухо, к которому источник ближе: без задержки
   Дальнее ухо: читать из delay line с delaySamples назад
   Записать текущий sample в delay line
   ```
3. Ветка `ITD_ILD`: применить и delay, и gains одновременно

### Проверка

- [ ] ITD: при перемещении источника слева направо ощущается "движение", даже при одинаковой громкости L/R
- [ ] ITD_ILD: более выраженная локализация чем ILD или ITD по отдельности
- [ ] Нет артефактов (щелчков, треска) — delay line корректно работает
- [ ] headRadius slider меняет эффект в реальном времени

---

## Итерация B: + Parametric HRTF (Head Shadow)

**Цель:** Моделирование затенения головой — low-pass фильтр на дальнем ухе.

### SDK (`LivekitAudioSource.cs`)

1. Добавить one-pole low-pass filter state: `float lpfStateL, lpfStateR`
2. Cutoff зависит от угла: `cutoff = lerp(20000, shadowCutoffHz, abs(sin(azimuth)) * shadowStrength)`
3. Рассчитать коэффициент: `alpha = 1 / (1 + sampleRate / (2 * PI * cutoff))`
4. В `OnAudioFilterRead`:
   ```
   На ближнем ухе: без фильтра
   На дальнем ухе: y[n] = y[n-1] + alpha * (x[n] - y[n-1])
   ```
5. Elevation: `cutoff *= lerp(1, cos(elevation), elevationInfluence)`

### Проверка

- [ ] Звук сбоку/сзади: дальнее ухо заметно "приглушённее" на высоких частотах
- [ ] Перключение ILD → ITD_ILD → HRTF: заметное улучшение локализации
- [ ] Нет артефактов при резкой смене углов
- [ ] Параметры cutoff/strength настраиваются из Inspector

---

## Файлы проекта

### SDK (LiveKit fork)

| Файл | Изменения |
|------|-----------|
| `LivekitAudioSource.cs` | `SpatializationMode` enum, `SetSpatialAngles()`, Inspector параметры с Headers, ILD/ITD/HRTF в OnAudioFilterRead |

### Unity project

| Файл | Изменения |
|------|-----------|
| `ProximityPanCalculator.cs` | Вычисление azimuth + elevation вместо pan |
| `ProximityVoiceChatManager.cs` | `mono: spatial` → задать `spatializationMode` |
| `manifest.json` | `file:` ссылка на локальный SDK (dev) |

---

## Финализация (после всех итераций)

1. Push SDK на GitHub
2. Переключить `manifest.json` на Git URL
3. Оценить перенос `ProximityPanCalculator` в ECS для производительности (50+ участников)
4. Оценить FFI mono оптимизацию (чтение 1 канала из нативного слоя)
