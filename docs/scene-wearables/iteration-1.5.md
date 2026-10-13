# Scene wearables — итерация 1.5: другие игроки, локально

> Временная папка; удаляется целиком перед подачей PR. База: [doc.md](doc.md) (итерация 1), [research.md](research.md).
> Ветка: `feat/sdk/scene-wearables`. Тестовая сцена: `MyScenes/Jubensha2` (ветка `scene-wearables`).

## Статус итерации 1

Закоммичена (`f8fa3346e6`, GPG). **Не скомпилирована и не проверена в Unity**: Rider не видел новые файлы до регенерации csproj. Ручной QA в LSD не проводился. Перед стартом 1.5 пользователь открывает Unity → компиляция → EditMode-фикстуры итерации 1 (список в doc.md § «Тесты»).

## Цель

Сцена переодевает **любого игрока в сцене** (себя и других) scene-wearables. Каждый клиент применяет это **у себя локально**. Explorer ничего не рассылает: ни comms, ни catalyst, ни профиль.

Согласованность между клиентами обеспечивает **сама сцена**: синкает карту «игрок → наряд» (`syncEntity`, как маяк `Costume{roleId,address}` в Jubensha) и на каждом клиенте пишет `AvatarShape` на сущность каждого игрока.

Принцип итерации 1 сохраняется: меняем wearables на **настоящем риге аватара**, вся локомоция и эмоуты работают.

## Отличие от scene emote

Scene emote explorer **рассылает** (`PlayerEmote.urn` / Pulse `EmoteStart`). Scene wearable **не рассылается**. Что видит клиент, решает его копия сцены. Своё переодевание другие видят, только если их копия сцены пишет его на мою сущность → сценовый код симметричен: «для каждого игрока, включая себя, надеть наряд его роли».

## Контракт для сцены

```ts
// любой игрок в сцене: себя — engine.PlayerEntity, других — их сущности (32..255)
for (const [entity, identity] of engine.getEntitiesWith(PlayerIdentityData)) {
  const outfit = outfitByAddress(identity.address) // из синкнутого состояния сцены
  if (outfit) AvatarShape.createOrReplace(entity, { id: identity.address, wearables: outfit, emotes: [] })
  else AvatarShape.deleteFrom(entity)
}
```

- Строки `wearables` — те же scene-пути `*_<category>.glb` (lowercase `.glb`), см. doc.md. Файл male/female сцена выбирает по `AvatarBase.get(entity).bodyShapeUrn` **этого** игрока.
- Применяются только scene-wearables **своей** сцены (префикс URN своей сцены), catalyst-URN игнорируются — как в итерации 1.
- Сущность игрока брать заново каждый раз (ADR-245: id 32..255 переиспользуются с новой версией).
- Снять: `AvatarShape.deleteFrom(entity)`. Игрок ушёл с парселей сцены → движок удаляет только мультиплеерную сущность (`PlayerSceneCRDTEntity`), CRDT-сущность с `AvatarShape` остаётся → explorer снимает наряд сам (`HandlePlayerLeft`) и надевает обратно, когда игрок вернулся (новая мультиплеерная сущность с тем же CRDT id), без повторной записи сценой.

## Видимость (решения)

| Ситуация | Что видит мой клиент |
|---|---|
| Я и игрок X в сцене S, сцена S пишет наряд X | X в наряде |
| X в S, я в соседней сцене T (S загружена) | профиль X (применяет только **текущая** сцена — как nametag и как локальный оверрайд итерации 1) |
| X ушёл с парселей S | профиль X (мультиплеерная сущность X удалена; вернулся → наряд снова) |
| S у меня не загружена (далеко / LOD) | профиль X |
| Портативные экспириенсы | не поддерживаем (плагин для PX не инжектится) |

## Дизайн в explorer

Пути — относительно `Explorer/Assets/DCL/`.

### Факты, на которые опираемся
- Удалённые игроки экспонируются сцене только пока стоят на её парселях: `Multiplayer/SDK/Systems/GlobalWorld/PlayerCRDTEntitiesHandlerSystem.cs` (`ResolvePlayerCRDTScene`), id из диапазона `SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM..TO` (32..255), с версией.
- У удалённого игрока в мире сцены **две сущности с одним CRDT id**: созданная CRDT-мостом (сюда пишет сцена; удалённых игроков нет в `EntitiesMap`) и мультиплеерная (`PlayerSceneCRDTEntity` + `SDKProfile`).
- Резолв уже решён в `SDKComponents/AvatarNametag/Systems/PropagateSceneAvatarTagSystem.cs`: `ResolveTarget` → `NeedsPlayerScan` → `MatchPendingPlayers` (по CRDT id находит `PlayerSceneCRDTEntity`) → `SDKProfile.UserId` → `IReadOnlyEntityParticipantTable` → глобальная сущность аватара. Pending/Unreachable-семантика и ретрай через `IsDirty`.
- Прецедент структурных изменений глобального мира из сценовой системы: nametag делает `globalWorld.AddOrSet(target, …)`.

### Изменения
1. **Резолв цели — переиспользовать nametag.** Вынести резолв «scene entity → global avatar entity» (local player / remote player по scan+UserId) в общий internal-хелпер, которым пользуются обе системы. Поведение nametag не меняется; его фикстуры должны проходить без правок. Если чистое извлечение не получается — зеркалить минимально и сказать об этом в отчёте.
2. **`PropagateSceneWearablesToPlayerSystem`** (переименовать в `PropagateSceneWearablesToAvatarsSystem`): вместо фильтра `PLAYER_ENTITY` — любой `CRDTEntity` = local player или 32..255.
   - Local player → как в итерации 1 (компонент создан заранее на `globalPlayerEntity`).
   - Remote → `globalWorld.AddOrSet(target, SceneWearablesOverride)` при первом применении, дальше мутация списка in place + `IsDirty`.
   - Маркер на сценовой сущности хранит **global target** (как `SceneAvatarTagApplied`): снятие/очистка бьют по нему. Если при повторном резолве target сменился (id переиспользован) — очистить старый.
   - Снятие (удаление компонента, `DeleteEntityIntention`, scene not current, finalize) = пустой список + `IsDirty` (компонент не удаляем). Если target уже мёртв — пропуск.
   - Тот же гейт `IsCurrent` для всех.
3. **`AvatarLoaderSystem`**: удалённый путь `UpdateAvatarFromProfile` мержит оверрайд (как main-player путь); новая query `[All(SceneWearablesOverride)][None(PlayerComponent, PBAvatarShape, DeleteEntityIntention)]` пересобирает удалённый аватар по `IsDirty`. Создание удалённого аватара (`CreateAvatarShapeFromProfile`) тоже учитывает оверрайд, если он уже есть.
4. **`AvatarShapeHandlerSystem.LoadAvatarShape`**: пропускать и диапазон 32..255 (иначе при записи сценой `Transform` на сущность игрока спаунится NPC-двойник). Проверить `PartitionAssetEntitiesSystem` / другие системы, которые реагируют на `PBAvatarShape` на CRDT-сущностях игроков.
5. **Удалённый игрок отключился** (глобальная сущность удаляется) — оверрайд умирает вместе с сущностью; сценовый маркер при следующей очистке видит мёртвый target и пропускает.
6. Не затрагиваем: Profile, comms, `PBAvatarEquippedData`, паспорт, бэкпак.

### Тесты (EditMode)
- `PropagateSceneWearablesToAvatarsSystemShould`: remote apply (через `PlayerSceneCRDTEntity` + `SDKProfile` + participant table), remote removal, scene not current, finalize, id переиспользован другим игроком, target мёртв, catalyst-URN и чужие scene-URN игнорируются. Шаблон — `PropagateSceneAvatarTagSystemShould`.
- `AvatarLoaderSystemShould`: remote merge + rebuild по dirty.
- `AvatarShapeHandlerSystemShould`: id 32..255 не спаунит NPC.
- Nametag-фикстуры — без изменений, проходят.

## Тестовая сцена (Jubensha2)

- `sceneWearables.ts`: синк-сущность на игрока `SceneOutfit{address, wearables: string[]}` (или переиспользовать `Costume`-маяк), гард `isStateSyncronized()`, `syncEntity` только внутри `main()` — по правилам из `costumeSync.ts`.
- Система: для каждой сущности с `PlayerIdentityData` — найти наряд по адресу, выбрать m/f по её `AvatarBase`, `createOrReplace` / `deleteFrom`.
- UI-кнопки итерации 1 теперь меняют **синкнутое** состояние своего игрока, а не пишут `AvatarShape` напрямую.
- NPC-манекен — без изменений.
- QA (2 клиента, оба в LSD на одной сцене; второй клиент — второй инстанс explorer / другой аккаунт — уточнить у пользователя способ):
  1. A надевает худи → A видит на себе, B видит на A.
  2. Анимации A у B (бег, прыжок, глайд) — wearable не отваливается.
  3. A уходит с парселей → у B наряд с A снят; возвращается → надет.
  4. B заходит позже → сразу видит A в наряде (late join через синк сцены).
  5. B в соседней сцене → видит профиль A.
  6. Бэкпак/паспорт — реальные профили.

## Риски

- Без синка в сцене клиенты видят разное — это контракт, а не баг.
- Каждое переодевание удалённого игрока — полная пересборка его аватара на каждом клиенте; при массовом переодевании (старт акта) — всплеск инстанцирования (бюджеты `AvatarInstantiatorSystem`).
- Исчерпание id 32..255 (>224 игроков) — игрок не экспонируется сцене → его не переодеть (существующее ограничение).
- Абьюз: сцена переодевает других только на экранах клиентов, где она запущена; класс риска как у `AvatarAttach`.
- Удалённый игрок с female body shape, а сцена выбрала male-файл → wearable всё равно наденется (unisex DTO), но может сидеть криво — ответственность сцены.

## Порядок работ (после компакта)

1. Пользователь: Unity → компиляция итерации 1 → EditMode-фикстуры итерации 1 → (опц.) ручной QA итерации 1. Найденное — чиним первым.
2. Агент E (`dcl-explorer-specialist`): explorer-изменения 1–6 + тесты. Жёсткое правило: **без Unity**, проверка только Rider (`get_file_problems`, `get_project_problems`, build).
3. Параллельно агент F (`dcl-test-scene-specialist`): Jubensha2 по § «Тестовая сцена», `npm run build`, без deploy/commit.
4. Агент-ревьюер (read-only, «как компилятор» + корректность + стандарты).
5. Фиксы ревью → коммит с GPG-подписью, без push.
