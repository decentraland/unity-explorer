# Scene wearables — прототип, итерация 1

> Временная папка; удаляется целиком перед подачей PR. Исследование: [research.md](research.md).
> Ветка: `feat/sdk/scene-wearables` (от `dev`). Тестовая сцена: `C:\DCL\Pravus-Orchester\MyScenes\Jubensha2`.

## Цель

Доказать, что wearable из **контента самой сцены** может:
1. переодеть **локального игрока** (только у него на экране);
2. переодеть **NPC** (`AvatarShape` в сцене) — «игрушка», где по клику меняются wearables.

**Принципиально:** меняются wearables **на настоящем риге аватара игрока**, а не подменяется весь аватар. Scene-wearable скиннится GPU-скиннингом на те же кости, поэтому вся локомоция и эмоуты (idle, бег, прыжок, полёт/глайд, падение, эмоуты) работают «бесплатно». Подходы из Jubensha (спрятать аватар через `AMT_HIDE_AVATARS` + GLB-двойник, анимируемый сценой) — коряво, работают пара анимаций; их не повторяем.

## Скоуп

| В итерации 1 | Вне итерации 1 |
|---|---|
| LSD (`--local-scene`), raw glTF | Мультиплеер (другие видят профиль как есть) |
| Деплойный AB-путь — код пишем, проверяем только если зальём сцену в мир | Изменения protocol / js-sdk-toolchain |
| Категории с GLB (hat, upper_body, lower_body, feet, helmet, …) | Facial features (eyes/mouth/eyebrows — PNG), `body_shape` |
| Один GLB на wearable, body shape выбирает сцена | Метаданные hides/replaces/representations (JSON / proto) |
| Снятие при выходе из сцены / удалении компонента | Permissions, модерация, лимиты, валидация рига |
| | Чистка памяти по выгрузке сцены |

## Контракт для сцены (без изменений протокола)

### Ссылка на wearable
- Строка в `PBAvatarShape.wearables`, оканчивающаяся на `.glb` (только нижний регистр: raw-путь `WearablePolymorphicBehaviour` матчит `.glb` case-sensitive), — **путь к файлу в контенте сцены** (как `expressionTriggerId` для scene emotes).
- Категория — **суффикс имени файла** `_<category>.glb`, по аналогии с `_emote.glb`. Матчинг по списку `WearableCategories` с самым длинным совпадением: `hoodie_m_upper_body.glb` → `upper_body`, `cap_hat.glb` → `hat`.
- Неизвестная категория / файла нет в контенте → wearable пропускается с `ReportHub` warning.
- Body shape: GLB считается unisex (одна representation на оба body shape). Если у wearable разные файлы для male/female — сцена сама выбирает файл по `AvatarBase.get(engine.PlayerEntity).bodyShapeUrn` (для NPC body shape сцена задаёт сама).

### NPC
```ts
AvatarShape.create(npc, {
  id: 'npc-1', bodyShape: 'urn:decentraland:off-chain:base-avatars:BaseMale',
  wearables: ['urn:decentraland:off-chain:base-avatars:eyes_00', 'assets/wearables/hoodie_m_upper_body.glb'],
  emotes: []
})
```
Catalyst-URN и пути сцены можно смешивать.

### Локальный игрок
```ts
AvatarShape.createOrReplace(engine.PlayerEntity, { id: 'self', wearables: ['assets/wearables/cap_hat.glb'], emotes: [] })
AvatarShape.deleteFrom(engine.PlayerEntity) // снять
```
- SDK уже позволяет писать компоненты на `PlayerEntity` (так работают `InputModifier`, `AvatarLocomotionOverrides`, nametag), PUT доходит до explorer.
- Семантика: **оверлей поверх профиля**. Каждый wearable из списка заменяет профильный той же категории; остальные поля `PBAvatarShape` на игроке игнорируются. Из списка берутся только пути этой сцены (`.glb` → URN с префиксом `scene-wearable:{sceneId}-`); catalyst-URN и scene-wearable URN других сцен на игроке игнорируются (иначе сцена могла бы надеть чужой NFT).
- Применяется, пока сцена `IsCurrent`. Вышел из сцены → снято; вернулся → надето снова.

### Требования к GLB
- Только `SkinnedMeshRenderer`, риг — DCL avatar armature (как у любого wearable). Статичные меши вырезаются.
- Тестовые ассеты — GLB из коллекции `base-avatars` с catalyst (`/content/entities/active` → `/content/contents/<hash>`).

## Дизайн в explorer

Пути — относительно `Explorer/Assets/DCL/`.

### Идентичность и DTO
- URN: `urn:decentraland:off-chain:scene-wearable:{sceneKey}-{hash}` — 5 частей, `URN.Shorten()` не режет. `sceneKey` = `SceneEntityDefinition.id` (в LSD `SceneShortInfo.Name` и так равен ему). Обратно URN не парсим (мультиплеера нет).
- `SceneWearableDTOFactory` (новый, `AvatarRendering/Wearables/Helpers/`): `TryCreate(ISceneData, string path, bool local, out URN, out WearableDTO)`.
  - `metadata.id = urn`, `data.category` из суффикса, `hides/replaces/removesDefaultHiding/tags = []`, `thumbnail = ""`.
  - `content` = main GLB + все файлы контента сцены из той же папки (на случай внешних текстур).
  - Одна representation: `bodyShapes = [BaseMale, BaseFemale]`, `mainFile = path`, `contents = [path]`.
  - LSD → подкласс `SceneWearableDTO` с `ContentDownloadUrl = SceneContent.ContentBaseUrl` (raw путь). Деплой → `id = SceneEntityDefinition.id` (идёт в `parentEntityID` AB), `assetBundleManifestVersion` = манифест сцены; манифест `null` / failed → wearable пропускается с warning (иначе `ResolveWearablePromisesSystem` ждёт вечно).
- Посев: `IWearableStorage.GetOrAddByDTO(dto, qualifiedForUnloading:false)`; если в сторадже уже лежит неуспешная запись — перезаписать. **Посев строго до** того, как URN попадёт в промис (иначе URN уйдёт в `/entities/active`, закэшируется как failed).

### Системы и плагин
Новый world-плагин `SceneWearablesPlugin`, регистрируется в `Infrastructure/Global/Dynamic/DynamicWorldContainer.cs` рядом с `SceneMaskedEmotePlugin` (там доступен `WearableCatalog` / `IWearableStorage`, global world и player entity). Для PX не инжектится.

1. **`ResolveSceneWearablePathsSystem`** (scene world, `ComponentInstantiationGroup`, `[UpdateBefore(AvatarShapeHandlerSystem)]`).
   Для любого entity с `PBAvatarShape.IsDirty`: каждую `.glb`-строку в `Wearables` → фабрика → посев → **замена строки на URN in-place**. Идемпотентно (URN уже не оканчивается на `.glb`). Так `AvatarShapeHandlerSystem` / `AvatarLoaderSystem` для NPC менять почти не нужно.
2. **`PropagateSceneWearablesToPlayerSystem`** (scene world, по образцу `SDKComponents/AvatarLocomotionOverrides/Systems/PropagateAvatarLocomotionOverridesSystem.cs` и `InputModifierHandlerSystem`):
   - query `CRDTEntity.Id == PLAYER_ENTITY` + `PBAvatarShape.IsDirty` → берёт scene-wearable URN из списка → `globalWorld` `AddOrSet(globalPlayer, SceneWearablesOverride{Urns, IsDirty=true})` + маркер на сценовой сущности;
   - удаление компонента (`[None(PBAvatarShape)][All(marker)]`) → пустой оверрайд;
   - `ISceneIsCurrentListener`: false → пустой оверрайд + снять маркер (только если применяла эта сцена), true → пометить PB dirty для повторного применения;
   - `IFinalizeWorldSystem` → пустой оверрайд только при маркере (выгрузка покинутой сцены не стирает оверрайд текущей).
3. **`AvatarShapeHandlerSystem.LoadAvatarShape`**: `if (crdtEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;` — иначе за игроком побежит NPC-дубль.

### Глобальный мир
- Компонент **`SceneWearablesOverride { List<URN> Urns; bool IsDirty }`** на global player entity (`AvatarRendering/AvatarShape/Components/`). Не удаляется, «снято» = пустой список + dirty (без структурных изменений).
  **Нельзя** класть `PBAvatarShape` на global player — на `[None(typeof(PBAvatarShape))]` держится различие NPC/игрок во многих системах.
- **`AvatarLoaderSystem`**:
  - main-player `CreateWearablePromise` дописывает `override.Urns` после `profile.Avatar.Wearables` (оба пути: create и update);
  - новая query `[All(PlayerComponent, SceneWearablesOverride)][None(PBAvatarShape, DeleteEntityIntention)]`: если `IsDirty` → `ApplyProfileToAvatarShape` (без `profile.IsDirty` — он дёргает пропагацию профиля/аналитику/эмоуты), сбросить флаг.
- **Дедуп по категории, last wins** в `WearableComponentsUtils.ExtractVisibleWearables`: `ExtractVisibleWearables` сейчас рендерит два wearable одной категории. Оверрайд дописан последним → побеждает. Затрагивает всех аватаров, но валидный профиль не содержит дублей категорий.
- В `Profile`, `IEquippedWearables`, `PBAvatarEquippedData` оверрайд **не попадает** — деплой профиля, паспорт, бэкпак видят реальный профиль.

### LSD gating
Сейчас raw-wearables работают только с `--self-preview-builder-collections`. Открываем их и для LSD:
- `PluginSystem/Global/WearablePlugin.cs:59,80-81` — `FinalizeRawWearableLoadingSystem` при `SelfPreviewBuilderCollections || LocalSceneDevelopment`;
- `PluginSystem/Global/DefaultTexturesContainer.cs:84` — RGBA32 texture arrays при том же условии (стоимость GPU-памяти — только в LSD, приемлемо).
- `GlobalGLTFLoadingPlugin` в LSD уже есть и качает по `realmData.Ipfs.ContentBaseUrl + hash` — это ровно контент локальной сцены.

## Тестовая сцена (Jubensha2)

- Ветка `scene-wearables` в репо Jubensha2 (старые костюмы на Mixamo-ригах — можно ломать/выключать).
- `assets/wearables/` — 4–6 base-avatar wearables разных категорий (hat, upper_body, lower_body, feet, eyewear/helmet), male + female варианты, переименованы под `_<category>.glb`.
- `src/modules/sceneWearables.ts`:
  - **NPC-манекен** с `AvatarShape`; клик (`pointerEventsSystem`) по NPC циклит набор wearables (scene-пути вперемешку с base URN).
  - **UI-панель** (React-ECS): кнопки «надеть X» / «снять всё» для игрока через `AvatarShape` на `engine.PlayerEntity`, выбор файла по body shape игрока.
- Ручной QA-чеклист:
  1. NPC появляется, клик меняет одежду; scene-wearable рендерится с текстурами (не белый).
  2. Кнопка надевает на игрока; заменяет профильный wearable той же категории; второго аватара за игроком нет.
  3. «Снять всё» возвращает профиль.
  4. Выход за границы сцены → снято; возврат → надето.
  5. Бэкпак/паспорт показывают реальный профиль; после рестарта профиль не изменён.

## Тесты (EditMode)

- `SceneWearableDTOFactoryShould` — парсинг категорий (включая `upper_body` vs `body`), неизвестная категория, отсутствующий файл, LSD vs deployed поля DTO, URN ≤6 частей.
- `AvatarShapeHandlerSystemShould` — `PLAYER_ENTITY` не спаунит NPC.
- `ResolveSceneWearablePathsSystemShould` — `.glb` → URN + посев, идемпотентность.
- `PropagateSceneWearablesToPlayerSystemShould` — применение / удаление / IsCurrent / finalize (шаблон: `InputModifierHandlerSystemShould`).
- `AvatarLoaderSystemShould` — мерж оверрайда, ребилд по dirty.
- `AvatarHideShould` / `WearableComponentsUtils` — дедуп last wins.

## Риски / открытые вопросы

- **Порядок посева**: URN, попавший в промис до посева, навсегда failed. Защита — посев в той же системе, что и замена пути.
- **LSD-хэши**: content-versioned хэши дают новый URN на каждую правку GLB (старые DTO копятся — ок для прототипа). С path-only `b64-` хэшами (старые dev-серверы, `LocalSceneDevHashes.IsPathOnly`) отредактированный GLB сохраняет URN → в кэше остаются устаревшие DTO и ассет.
- **Деплойный AB-путь не доказан**: scene-AB могут дать нечитаемые меши / не тот формат текстур → белый wearable. Проверка требует деплоя Jubensha2 в мир — отдельное решение.
- `skin` не скрывает hat/mask/eyewear профиля; у синтетического DTO нет `hides`.
- Синтетический `upper_body` с пустым `removesDefaultHiding` прячет руки тела → у рубашек без меша кистей аватар без рук.
- `ForceRender` профиля может перебить скрытие.
- Дедуп по категории (last wins) в `ExtractVisibleWearables` действует для **всех** аватаров, не только для оверрайда.
- Плагин инжектится в каждую не-PX сцену в любом realm, без гейтинга. Только прототип — **в таком виде не мержить**.
- Внешние текстуры резолвятся только из папки самого GLB.
- Ассеты scene wearables никогда не выгружаются (`qualifiedForUnloading:false`, scene-scoped чистки нет).
- Несколько сцен пишут `PlayerEntity` — применяет только текущая; выгрузка покинутой сцены не трогает оверрайд текущей.

## Разбивка на саб-агентов

| # | Агент | Задача | Зависит от |
|---|---|---|---|
| A | `dcl-explorer-specialist` | Фундамент: `SceneWearableDTOFactory`, `SceneWearablesPlugin` + `ResolveSceneWearablePathsSystem`, фильтр `PLAYER_ENTITY`, LSD gating, тесты A | — |
| B | `dcl-test-scene-specialist` | Jubensha2: ветка, ассеты base-avatars, `sceneWearables.ts` (NPC + UI) | — (контракт выше) |
| C | `dcl-explorer-specialist` | Игрок: `SceneWearablesOverride`, `PropagateSceneWearablesToPlayerSystem`, мерж в `AvatarLoaderSystem`, дедуп, тесты C | A |
| D | review (`code-review` / Explore) | Ревью диффа против этого дока и code standards | A, C |

A и B — параллельно; C — после A; затем компиляция (Rider MCP) + EditMode-тесты; ручной прогон в LSD — пользователь.

## Бэклог следующих итераций

- Метаданные wearable: JSON-сайдкар (по модели `WearableJson`) → затем поле/компонент в protocol.
- Мультиплеер: состояние ведёт сцена (компонент с target address) vs новый comms-пакет.
- Facial features, `hides`, representations по body shape.
- Проверка/валидация рига, лимиты, permissions.
- Деплойный AB-путь + возможные правки конвертера.
- Scene-scoped чистка `WearableStorage`.
