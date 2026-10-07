# Scene wearables — исследование

> Временная папка на время прототипа; удаляется целиком перед подачей PR.
> Дата: 2026-10-07. Пути без префикса — относительно `Explorer/Assets/DCL/`.

Вопрос: сцена уже умеет давать **scene emote** (эмоут из собственного контента сцены). Насколько сложнее дать **scene wearable** — wearable из контента сцены, надеваемый на игрока / NPC?

## TL;DR

- Прецедентов нет нигде: protocol, js-sdk-toolchain, common-schemas, explorer, ADR — ни proto, ни URN-неймспейса, ни обсуждений. Greenfield.
- «Не сильно сложнее эмоутов» — правда **только для NPC** (`AvatarShape` в сцене): сцена исполняется у всех клиентов, мультиплеер и профиль не нужны.
- Для **игрока** заметно сложнее по трём фронтам: мультиплеер (wearable — состояние, а не событие; late join), override-слой поверх профиля, жёсткие требования к ассету.

## 1. Как устроены scene emotes (база для копирования)

### Вход из SDK
- `RestrictedActions.triggerSceneEmote({src, loop?, mask?})` → `Infrastructure/CrdtEcsBridge/JsModulesImplementation/RestrictedActions/RestrictedActionsAPIImplementation.cs:140-170`.
  - Гейты: только `sceneStateProvider.IsCurrent` и `sceneData.SceneContent.TryGetHash(src)`. **Permissions не проверяются** (`PI_ALLOW_TO_TRIGGER_AVATAR_EMOTE` — SDK6-only).
  - PX и smart wearables всегда `IsCurrent` (`Infrastructure/SceneRunner/PortableExperienceSceneFacade.cs:11`).
- `GlobalWorldActions.TriggerSceneEmoteAsync` (`.../RestrictedActions/GlobalWorldActions.cs:108-133`): LSD/builder → raw glTF (с проверкой `_emote.glb`), иначе → AB из манифеста сцены.
- NPC: `PBAvatarShape.expressionTriggerId` с путём `*.glb` → `SDKComponents/AvatarShape/Systems/AvatarShapeHandlerSystem.cs:134-183` (детект по суффиксу `.glb`, `SceneContent.TryGetHash`, промис scene emote; loop для NPC не поддержан, `:181`).

### Идентичность
- URN: `urn:decentraland:off-chain:scene-emote:{sceneId}-{hash}-{loop}` (`AvatarRendering/Emotes/Components/Intents/GetSceneEmoteFromRealmIntention.cs:20,56-57`). 5 частей.
- `URN.Shorten()` режет последнюю часть у URN с >6 частями (`CommunicationData/URLHelpers/URN.cs:131-165`) → синтетические URN держать ≤6 частей, без `:` в payload.
- Парсер `CharacterEmoteSystem.TryParseSceneEmoteURN` (`AvatarRendering/Emotes/Systems/Play/CharacterEmoteSystem.cs:603-660`) — string-split + префиксный скан всех загруженных сцен.

### Загрузка
- Синтетический `EmoteDTO` (`AvatarRendering/Emotes/Systems/Load/LoadSceneEmotesSystem.cs:90-135`): `id = urn`, `assetBundleManifestVersion` = манифест сцены, `content = [{file:"scene-emote.glb", hash}]`, одна representation на оба body shape. → `emoteStorage.GetOrAddByDTO`.
- AB: `GetAssetBundleIntention.FromHash(manifest.GetCdnRequestHash(hash), manifest, parentEntityID: sceneId)` (`GetSceneEmoteFromRealmIntention.cs:59-74`). Путь CDN `{v}/{sceneId}/{hash}` или `{v}/assets/{hash}` (`NetworkDefinitions/AssetBundleManifestVersion.cs:178-189`).
- Raw glTF (LSD): `GetGLTFIntention.Create(path, hash, mecanimAnimationClips:false)` — legacy-клипы.
- Финализация через тот же `WearablePolymorphicBehaviour.TryToConvertToRegularAsset`, что и у wearables.

### Конвертер (decentraland/asset-bundle-converter, не локально — читал через `gh api`)
- `AssetBundleConverter.cs:295-299`: `isEmote = entity.type ∋ "emote" || fileName.EndsWith("_emote.glb")` (`Utils.IsEmoteFileName`); `isWearable = entity.type ∋ "wearable"`.
- `GetAnimationMethod` (`:624-629`): emote → Mecanim (+ `CreateAnimatorController`), wearable → `None`, сцена → `settings.AnimationMethod` (Legacy).
- Шейдер один — `ShaderType.Dcl` по умолчанию для всех (`ClientSettings.cs`); текстуры `TextureImporterCompression.CompressedHQ` для всех (`Editor/CustomGltfImporter.cs:304-305`). Отдельной ветки для wearables кроме `AnimationMethod` не нашёл.

### Мультиплеер
- RFC-4 `Packet.SceneEmote{scene_entity_id, source}` есть в proto, но **мёртвый**: только зарегистрирован в `Multiplayer/Connections/Messaging/MessageWrap.cs:111` / `Pipe/MessagePipe.cs:206`.
- Реально URN едет в обычном `PlayerEmote.urn` (LiveKit, `Multiplayer/Emotes/LiveKitEmotesMessageBus.cs:64-102`) и `EmoteStart.emote_id` (Pulse, `Multiplayer/Movement/Systems/PulseMultiplayerBus.Emotes.cs:29-58`).
- Получатель резолвит сцену из своего `scenesCache`. **Нет сцены у получателя → эмоут молча не играет** (`CharacterEmoteSystem.cs:574-577`), висит до таймаута.

### Жизненный цикл
- Выход из сцены: `CharacterEmoteSystem.CancelSceneEmotesBySceneChange` (`:95-112`) — парсит URN каждый кадр, стопит если сцена не текущая (PX исключены).
- Кэш: scene emotes живут в глобальном `MemoryEmotesStorage`, не привязаны к жизни сцены.

## 2. Wearable-пайплайн (куда встраиваться)

### Резолв
- `AvatarLoaderSystem` → `GetWearablesByPointersIntention` (body shape — pointer[0]) → `ResolveWearablePromisesSystem` (`AvatarRendering/Wearables/Systems/ResolveWearablePromisesSystem.cs:58-153`).
- **Ключ:** если в `WearableStorage` уже лежит `Wearable` с `Model.Succeeded`, catalyst не дёргается (`:95-106`). Так работают body shapes (`Wearables/Systems/Load/LoadDefaultWearablesSystem.cs:84-148`, `GetOrAddByDTO(dto, qualifiedForUnloading:false)`) и builder preview.
- Иначе — `POST /entities/active` в asset-bundle registry; не найденные pointers тихо выкидываются навсегда (`FinalizeWearableLoadingSystemBase.cs:85-87`).

### DTO
- `WearableDTO` → `AvatarAttachmentDTO` → `EntityDefinitionBase`: `id` (entity id, он же AB `parentEntityID`), `content[] {file, hash}`, `assetBundleManifestVersion`, `metadata { id(urn), data { category, representations[], replaces, hides, removesDefaultHiding } }`.
- `Representation { bodyShapes[], mainFile, contents[], overrideHides[], overrideReplaces[] }`. Сравнение bodyShape — **case-sensitive** строка `urn:decentraland:off-chain:base-avatars:BaseMale|BaseFemale` (`Loading/Components/BodyShape.cs:53-54`).
- `ContentDownloadUrl` — protected setter (`Loading/DTO/AvatarAttachmentDTO.cs:20`); непустой → raw-путь.

### AB vs raw
- `WearablePolymorphicBehaviour.CreatePromise` (`AvatarRendering/Wearables/Helpers/WearablePolymorphicBehaviour.cs:188-235`): `ContentDownloadUrl` задан → raw (`.glb` → `GetGLTFIntention`, `.png` → текстура); иначе AB с `parentEntityID: DTO.id` (`:221-231`) — для scene-контента нужен **sceneId**.
- Raw-путь в проде выключен:
  - `FinalizeRawWearableLoadingSystem` только при `FeatureId.SelfPreviewBuilderCollections` (`PluginSystem/Global/WearablePlugin.cs:80-81`);
  - глобальный `LoadGLTFSystem` только при LSD или builder preview (`PluginSystem/Global/GlobalGLTFLoadingPlugin.cs:40-55`), один фиксированный base URL;
  - RGBA32 texture arrays только с builder-флагом (`PluginSystem/Global/DefaultTexturesContainer.cs:84`, `AvatarShape/Rendering/TextureArray/TextureArrayContainerFactory.cs:78-115`), аллоцируются заранее — оценочно сотни МБ GPU.

### Инстанцирование и требования к GLB
- Только `SkinnedMeshRenderer`: объекты с `MeshRenderer` **удаляются** (`AvatarRendering/Loading/Assets/AttachmentAssetUtility.cs:56-60`) — статичная шляпа исчезнет.
- Animator/Animation удаляются (`AttachmentAssetUtility.cs:78-80`) → Legacy-анимации scene-AB не мешают.
- Скиннинг без ремапа костей: индексы `BoneWeight` используются as-is против 62-костного DCL armature (`AvatarShape/ComputeShader/BufferContainer/ComputeSkinningBufferContainer.cs:115-118`); >62 — spring bones (до 256).
- Меш должен быть readable (CPU-чтение вершин/весов/bindposes).
- Материал → пуловый DCL_Toon; берутся только `_BaseColor`, `_EmissionColor`, alpha-clip/transparency, `_BaseMap`/`_BumpMap`/`_EmissionMap`.
- Texture arrays: текстура берётся, только если формат совпадает с хендлером (BC7/BC5 для AB, RGBA32 для raw); иначе **молча белая**.
- Body shape только Male/Female; кастомный body shape невозможен.

### Источник правды и override
- Игрок: `Profile` на player entity; `profile.IsDirty` → `ForgetLoading` → новый промис из `profile.Avatar.Wearables` (`AvatarRendering/AvatarShape/Systems/AvatarLoaderSystem.cs:112-165`). Естественная точка мержа — `CreateWearablePromise(Profile)`.
- Готового временного override-слоя нет:
  - `Profiles/Self/ForcedWearables.cs` — session-wide из launch args, отключает деплой профиля;
  - builder-preview бэкпак — локальный `Version++` профиля;
  - `Character/CharacterPreview` — отдельная сущность с произвольным списком URN;
  - `PBAvatarModifierArea` — только hide avatars / nametags / passports.
- `ExtractVisibleWearables` **не дедуплицирует по категории** → override обязан сам выкинуть профильный wearable той же категории.
- Если положить scene-URN в `Profile`, он утечёт в `IEquippedWearables` → `CreateNewProfileForUpdate` (деплой), `PBAvatarEquippedData`, паспорт, скриншоты.

### NPC
- `AvatarShapeHandlerSystem.LoadAvatarShape` (`:66-94`) создаёт зеркальную сущность в global world; `AvatarLoaderSystem` берёт `pbAvatarShape.Wearables` как pointers (`:153-156`).
- Неизвестные строки (`hat.glb`) уходят в `/entities/active`, падают и выкидываются. Пути к контенту сцены сейчас не поддержаны — но рядом уже есть ровно такой же трюк для `expressionTriggerId`.

### Память
- `WearableStorage` (`AvatarRendering/Wearables/Helpers/WearableStorage.cs`) по факту не выселяет (`Set()` не попадает в `listedCacheKeys`). Scene-scoped очистки нет.

## 3. Protocol / SDK / schemas

- `restricted_actions.proto`: `TriggerSceneEmoteRequest {src, loop?, mask?}`; wearable-аналогов нет.
- `PBAvatarShape` (1080): `wearables = 10 (repeated string)`, `emotes`, `expression_trigger_id`, `show_only_wearables`. Doc-коммент обещает подстановку дефолтов по слотам — в explorer её нет.
- `PBAvatarEquippedData` (1091): read-only для сцены, пишется из `profile.Avatar.Wearables`.
- `PBAvatarModifierArea`: `AMT_HIDE_AVATARS / AMT_DISABLE_PASSPORTS / AMT_HIDE_NAMETAGS` — wearable-модификатора нет.
- RFC-4 comms: профиль синкается как `AnnounceProfileVersion{version}` → получатель тянет подписанный профиль из catalyst. `ProfileRequest/ProfileResponse` заведены, но не используются. Канала для временных wearables нет; в Pulse тоже только версия.
- common-schemas: `Wearable.validate` принимает только base-avatar / standard / third-party. `WearableJson` (smart-wearable `wearable.json`) и `dapps/preview` wearable definitions — ближайшие модели «wearable не из catalyst».
- js-sdk-toolchain: preview-сервер (`sdk-commands/src/commands/start/server/endpoints.ts:68-108,347-405`) уже умеет dev-only инъекцию wearable в `/lambdas/profiles` с `urn:<uuid>` — прецедент синтетического wearable.
- `_emote.glb` в SDK/CLI не валидируется; контракт только в конвертере и в LSD-ветках explorer.

## 4. Что прямолинейно

1. Резолв без catalyst — посев синтетического `WearableDTO` в `WearableStorage`.
2. AB из манифеста сцены — уже есть, нужен `parentEntityID = sceneId`.
3. Конвертер, вероятно, не трогать: scene-AB и wearable-AB отличаются только `AnimationMethod`, а explorer всё равно режет Animator/Animation. **Не проверено**: readable meshes и формат текстур scene-AB.
4. NPC — по образцу `expressionTriggerId` `*.glb`; без мультиплеера и профиля.

## 5. Подводные камни (по убыванию)

1. **Мультиплеер + late join.** Эмоут — событие, wearable — состояние; LiveKit не умеет реплей. Варианты: (a) новый пакет RFC-4 + Pulse + ребродкаст; (b) состояние ведёт сцена (у каждого клиента своя копия решает, что надето на каждом игроке; синк — забота сцены). Сцена у получателя нужна в обоих → (b) дешевле.
2. **Override-слой** поверх профиля: мерж, дедуп по категории, неутечка в деплой/`PBAvatarEquippedData`/паспорт/бэкпак.
3. **Снятие при выходе**: телепорт, unload, PX; каждое переодевание = полный ребилд аватара (бюджеты, мерцание).
4. **Требования к GLB** (см. §2): skinned only, DCL armature, readable, формат текстур, Male/Female.
5. **Метаданные** (category, hides, replaces, representations) — у эмоута их нет; в URN не влезут.
6. **LSD сложнее, чем у эмоутов**: raw-wearable путь и RGBA32 arrays за builder-флагом.
7. Facial features и `body_shape` — вне MVP.
8. Память: scene-scoped очистка — новый код.
9. Модерация: визуально то же, что уже даёт `AvatarAttach` с произвольным GLB; ценность scene wearables — деформация по телу и hides категорий.
10. Схемы: новый вариант в `Wearable.validate`, если где-то понадобится валидация.

## 6. Найдено попутно в scene emotes (не копировать)

По отчётам субагентов, вручную не перепроверено:
- Realm-путь отдаёт `success:true` при упавшем ассете (`LoadSceneEmotesSystem.cs:145-149` — failure только у local).
- `AddReference` без парного `Dereference` → scene emotes не выселяются (`LoadSceneEmotesSystem.cs:143`).
- Орфанные promise-сущности из `CharacterEmoteSystem.CreateEmotePromise` (никто не потребляет scene-intent результаты).
- Нет `finally` в `GlobalWorldActions.TriggerSceneEmoteAsync` → `CharacterWaitingSceneEmoteLoading` (к тому же никем не читается) остаётся при отмене.
- Fallback sceneId = `DecodedBase.ToString()` → `"(x, y)"` (`GlobalWorldActions.cs:127`).
- Два дубликата константы `_emote.glb` (`GlobalWorldActions.cs:32`, `AvatarShapeHandlerSystem.cs:36`).
- `GlobalWorldActionsShould.ShouldNotTriggerSceneEmoteIfAvatarNotVisible` проходит тривиально.
