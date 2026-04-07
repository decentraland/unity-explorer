# Guide: Extracting Context Menu from `feat/social-emotes-rebirth`

**Source branch:** `origin/feat/social-emotes-rebirth`
**Target branch:** `feat/proximity-voice-chat`
**Base:** `origin/dev`

## Summary

The `feat/social-emotes-rebirth` branch (70 commits, 272 changed files) contains a complete
social emotes system. The context menu was fixed/improved as a side effect.
Our goal is to extract **only** the context menu functionality (right-click on avatar ->
profile context menu with proper cursor handling), **without** any social emotes logic.

---

## 1. FILES TO TAKE FULLY (clean, no social emote contamination)

### 1.1 Cursor / PointerLock infrastructure (CursorState.LockedWithUI)

These files introduce the `LockedWithUI` cursor state, which allows opening a UI menu
while the camera stays locked (mouse visible, camera frozen). This is the core fix
for the context menu working properly in locked-camera mode.

| File | Change type | Description |
|------|-------------|-------------|
| `Explorer/Assets/DCL/Character/CharacterCamera/Components/CursorComponent.cs` | Modified | Adds `CursorState.LockedWithUI` enum value |
| `Explorer/Assets/DCL/Input/Component/PointerLockIntention.cs` | Modified | Adds `WithUI` bool field to distinguish menu-triggered unlock |
| `Explorer/Assets/DCL/Input/Systems/UpdateCursorInputSystem.cs` | Modified | Full `LockedWithUI` state handling: cursor style, visibility, crosshair |
| `Explorer/Assets/DCL/Character/CharacterCamera/Systems/ControlCinemachineVirtualCameraSystem.cs` | Modified (1 line) | Early return when `LockedWithUI` (freeze camera offset) |
| `Explorer/Assets/DCL/Character/CharacterCamera/Systems/UpdateCameraInputSystem.cs` | Modified (1 line) | Zero camera delta when `LockedWithUI` |

### 1.2 Hover tooltip fix

| File | Change type | Description |
|------|-------------|-------------|
| `Explorer/Assets/DCL/Interaction/HoverCanvas/UI/HoverCanvasTooltip.uxml` | Modified | `ui:Image` -> `ui:VisualElement` for Icon element |
| `Explorer/Assets/DCL/Interaction/HoverCanvas/UI/HoverCanvasTooltipElement.cs` | Modified | `Image` -> `VisualElement` for `inputIcon` field |

### 1.3 MVC facade (isOpenedOnWorldAvatar parameter)

| File | Change type | Description |
|------|-------------|-------------|
| `Explorer/Assets/DCL/Infrastructure/MVC/MVCFacade/MVCManagerMenusAccessFacade.cs` | Modified | Adds `isOpenedOnWorldAvatar` param + `onHide` callback forwarding |

---

## 2. FILES TO TAKE WITH PARTIAL EXTRACTION (need manual cherry-pick / adaptation)

### 2.1 `ProcessOtherAvatarsInteractionSystem.cs`
**Path:** `Explorer/Assets/DCL/Interaction/Systems/ProcessOtherAvatarsInteractionSystem.cs`

**TAKE:**
- Right-click handler `OpenOptionsContextMenu` — opens profile context menu
- `OnContextMenuClosed` — restores cursor lock state
- `OnPlayerMoved` — closes menu on WASD/Jump
- Constructor: subscribe to `dclInput.Player.RightPointer.performed` and `Movement/Jump.performed`
- Dispose: unsubscribe all handlers
- Dependencies: `IWeb3IdentityCache`, `ObjectProxy<Entity>` (camera), `Entity` (player)
- Tooltip changed from "View Profile" to "Options" on `RightPointer`

**SKIP:**
- Left-click handler `OpenEmoteOutcomeContextMenu` — social emote outcomes UI
- `OnOutcomePerformed` — sends `MoveBeforePlayingSocialEmoteIntent`
- `socialEmoteInteractionTooltip` — social emote tooltip on left-click
- All `SocialEmoteInteractionsManager` references
- `SocialEmoteOutcomesContextMenuSettings` / `SocialEmotesSettings` dependencies
- `contextMenuConfiguration` (GenericContextMenu) — used only for emote outcomes
- Distance checks using `socialEmotesSettings.VisibilityDistance` / `InteractionDistance`
- `ShowAvatarHighlightIntent` additions

**NOTE:** The original dev version uses left-click (`Pointer`) for context menu.
The social-emotes branch changes it to right-click (`RightPointer`) for context menu
and repurposes left-click for emote outcomes. We want the right-click behavior.

### 2.2 `GlobalInteractionPlugin.cs`
**Path:** `Explorer/Assets/DCL/PluginSystem/Global/GlobalInteractionPlugin.cs`

**TAKE:**
- New constructor params: `IWeb3IdentityCache`, `ObjectProxy<Entity>` (camera), `Entity` (player)
- Updated `InjectToWorld` call for `ProcessOtherAvatarsInteractionSystem` with new deps
- Remove `IMVCManager` from constructor (replaced by new deps)

**SKIP:**
- `SocialEmoteOutcomesContextMenuSettings` / `SocialEmotesSettings` in Settings class
- `AvatarHighlightSystem.InjectToWorld` call
- `InteractionSettingsData` in Settings class (unless taking avatar highlight too)

### 2.3 `DynamicWorldContainer.cs`
**Path:** `Explorer/Assets/DCL/Infrastructure/Global/Dynamic/DynamicWorldContainer.cs`

**TAKE:**
- Updated `GlobalInteractionPlugin` construction: pass `identityCache`, `cameraEntityProxy`, `playerEntity` instead of `mvcManager`

**SKIP:**
- `EphemeralNotificationsController` creation and registration
- `SocialEmoteInteractionsManager.Initialize(...)` and debug view wiring
- Changes to `EmotePlugin` constructor (extra social emote deps)
- Changes to `FriendsContainer` constructor (ephemeral notifications)

### 2.4 `GenericUserProfileContextMenuController.cs`
**Path:** `Explorer/Assets/DCL/UI/GenericContextMenu/GenericUserProfile/GenericUserProfileContextMenuController.cs`

**TAKE:**
- `isOpenedOnWorldAvatar` parameter in `ShowUserProfileContextMenuAsync`
- When `isOpenedOnWorldAvatar == true`: disable Mention button, disable Jump In button, disable community invitation button
- `invitationButton` reference from `AddSubmenuControlToContextMenu` return value

**SKIP:**
- `socialEmoteButtonControlSettings` field and "Social Emote" button creation
- `contextMenuSocialEmoteButton` field
- `OnSocialEmoteButtonClicked` method
- `EmotesWheelParams` import and usage
- Enable/disable of social emote button

### 2.5 `CommunityInvitationContextMenuButtonHandler.cs`
**Path:** `Explorer/Assets/DCL/UI/GenericContextMenu/GenericUserProfile/CommunityInvitationContextMenuButtonHandler.cs`

**TAKE:**
- Changed return type: `AddSubmenuControlToContextMenu` now returns `GenericContextMenuElement`
  (needed for the `invitationButton` reference in the controller)

### 2.6 `GenericUserProfileContextMenuSettings.cs` + `.asset`

**SKIP** the `SocialEmoteButtonConfig` property — it's only for social emotes.
No other changes needed from these files.

---

## 3. FILES TO SKIP ENTIRELY (social emotes only)

### 3.1 New social emote systems & components
```
Explorer/Assets/DCL/AvatarRendering/Emotes/Components/Intents/*  (all new intents)
Explorer/Assets/DCL/AvatarRendering/Emotes/SocialEmoteInteractionsManager.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/SocialEmotesSettings.asset
Explorer/Assets/DCL/AvatarRendering/Emotes/Systems/SocialEmoteInteractionSystem.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Systems/SocialEmotePinsSystem.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Systems/SocialEmotesSettings.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/UI/SocialEmoteOutcomesContextMenuSettings.*
Explorer/Assets/DCL/AvatarRendering/Emotes/UI/SocialEmotePin.*
Explorer/Assets/DCL/AvatarRendering/Emotes/DebugSocialEmoteInteractions*
Explorer/Assets/DCL/AvatarRendering/Emotes/MoveToOutcomeStartPositionIntent.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Systems/Play/AvatarStateMachineEventHandler.cs
Explorer/Assets/DCL/AvatarRendering/AvatarShape/UnityInterface/AvatarStateMachineBehaviour.cs
Explorer/Assets/DCL/Multiplayer/Emotes/LookAtPositionIntention.cs
```

### 3.2 Modified social emote systems
```
Explorer/Assets/DCL/AvatarRendering/Emotes/Systems/Play/CharacterEmoteSystem.cs  (+740 lines)
Explorer/Assets/DCL/AvatarRendering/Emotes/Systems/Play/EmotePlayer.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Systems/RemoteEmotesSystem.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Systems/UpdateEmoteInputSystem.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Systems/FinalizeEmoteLoadingSystem.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Components/CharacterEmoteComponent.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Components/CharacterEmoteIntent.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Components/EmoteReferences.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Components/IEmote.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Emote.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Helpers/DTO/EmoteDTO.cs
Explorer/Assets/DCL/AvatarRendering/Emotes/Helpers/EmotesBus.cs
Explorer/Assets/DCL/Multiplayer/Emotes/MultiplayerEmotesMessageBus.cs
Explorer/Assets/DCL/Multiplayer/Emotes/RemoteEmoteIntention.cs
Explorer/Assets/DCL/AvatarRendering/AvatarShape/Components/AvatarShapeComponent.cs
Explorer/Assets/DCL/AvatarRendering/AvatarShape/UnityInterface/AvatarBase.cs
```

### 3.3 RestrictedActions (emote trigger refactoring)
```
Explorer/Assets/DCL/Infrastructure/CrdtEcsBridge/.../GlobalWorldActions.cs
Explorer/Assets/DCL/Infrastructure/CrdtEcsBridge/.../IGlobalWorldActions.cs
Explorer/Assets/DCL/Infrastructure/CrdtEcsBridge/.../RestrictedActionsAPIImplementation.cs
Explorer/Assets/DCL/Infrastructure/CrdtEcsBridge/.../RestrictedActionsAPIWrapper.cs
Explorer/Assets/DCL/Infrastructure/CrdtEcsBridge/.../Tests/GlobalWorldActionsShould.cs
```

### 3.4 Emotes wheel changes
```
Explorer/Assets/DCL/EmotesWheel/Params/*  (new assembly)
Explorer/Assets/DCL/EmotesWheel/EmotesWheelController.cs
Explorer/Assets/DCL/EmotesWheel/EmotesWheelView.cs
Explorer/Assets/DCL/EmotesWheel/EmoteWheelSlotView.cs
Explorer/Assets/DCL/EmotesWheel/Assets/*.prefab
Explorer/Assets/DCL/EmotesWheel/Textures/*
```

### 3.5 Ephemeral notifications system (replaces FriendPushNotification)
```
Explorer/Assets/DCL/UI/EphemeralNotifications/*  (entire new module)
Explorer/Assets/DCL/Friends/UI/PushNotifications/FriendPushNotificationController.cs
Explorer/Assets/DCL/Friends/UI/PushNotifications/FriendPushNotificationView.cs (deleted)
```

### 3.6 Avatar highlight system (tied to social emote intents)
```
Explorer/Assets/DCL/Interaction/Systems/AvatarHighlightSystem.cs  (new)
Explorer/Assets/DCL/Interaction/Settings/InteractionSettingsData.cs  (avatar outline fields)
Explorer/Assets/DCL/Interaction/Settings/InteractionSettingsData.asset
```
> Note: AvatarHighlightSystem depends on `ShowAvatarHighlightIntent` and
> `PlayAvatarHighlightBlinkingAnimationIntent` which are social emote components.
> If avatar outline is desired later, these intents must also be extracted.

### 3.7 Social emote textures
```
Explorer/Assets/Textures/Common/KeyContainer.png
Explorer/Assets/Textures/Common/SendEmote.png
Explorer/Assets/Textures/Common/SocialEmotePinArrow.png
Explorer/Assets/Textures/Common/SocialEmotePinIcon.png
Explorer/Assets/DCL/Backpack/Textures/SocialEmotesIcon.png
Explorer/Assets/DCL/EmotesWheel/Textures/SocialEmotesIcon.png
```

### 3.8 Character motion / multiplayer changes
```
Explorer/Assets/DCL/Character/CharacterMotion/Components/MovementInputComponent.cs
Explorer/Assets/DCL/Character/CharacterMotion/Components/PlayerTeleportIntent.cs
Explorer/Assets/DCL/Character/CharacterMotion/Systems/CalculateCharacterVelocitySystem.cs
Explorer/Assets/DCL/Character/CharacterMotion/Systems/HeadIKSystem.cs
Explorer/Assets/DCL/Character/CharacterMotion/Systems/RotateCharacterSystem.cs
Explorer/Assets/DCL/Character/CharacterMotion/Systems/TeleportCharacterSystem.cs
Explorer/Assets/DCL/Character/CharacterMotion/Systems/UpdateInputMovementSystem.cs
Explorer/Assets/DCL/Character/Components/CharacterTransform.cs
Explorer/Assets/DCL/Multiplayer/Movement/Systems/*
```

### 3.9 Other unrelated changes
```
Explorer/Assets/DCL/PluginSystem/Global/EmotePlugin.cs  (social emote pool/system wiring)
Explorer/Assets/DCL/PluginSystem/Global/FriendsContainer.cs  (ephemeral notif dep)
Explorer/Assets/DCL/Infrastructure/Global/Dynamic/MainSceneLoader.cs  (MultiLogger)
Explorer/Assets/DCL/PerformanceAndDiagnostics/Diagnostics/MultiLogger.cs
Explorer/Assets/DCL/PerformanceAndDiagnostics/Diagnostics/ReportsHandling/ReportCategory.cs
Explorer/Assets/DCL/Infrastructure/Utility/GizmoDrawer.cs
Explorer/Assets/DCL/UI/MainUIContainer/*  (EphemeralNotifications wiring)
Explorer/Assets/DCL/UI/Sidebar/*  (EmotesWheelParams)
Explorer/Assets/DCL/UI/SharedSpaceManager/*  (EmotesWheelParams)
Explorer/Assets/DCL/Social/DCL.Social.asmdef  (social emote assembly refs)
Explorer/Assets/DCL/PluginSystem/DCL.Plugins.asmdef  (social emote assembly refs)
Explorer/Assets/DCL/UI/MainUIContainer/MainUi.asmdef  (social emote assembly refs)
Explorer/Assets/DCL/SceneLoadingScreens/*  (texture swaps, unrelated)
Explorer/Assets/Settings/PlayMode/Multiplayer.asset  (new asset, unrelated)
All .prefab / .controller / binary asset changes in Emotes/AvatarShape
```

---

## 4. RECOMMENDED EXTRACTION APPROACH

**Option A: Manual re-implementation (recommended)**
1. Apply Section 1 files via `git checkout origin/feat/social-emotes-rebirth -- <path>`
2. Manually rewrite `ProcessOtherAvatarsInteractionSystem.cs`:
   - Keep right-click opens profile context menu
   - Keep cursor lock/unlock logic (`LockedWithUI`)
   - Keep close-on-movement
   - Drop ALL social emote references
   - Dependencies: `IWeb3IdentityCache`, `ObjectProxy<Entity>` camera, `Entity` player
   - Do NOT need `SocialEmotesSettings`, `SocialEmoteOutcomesContextMenuSettings`, `GenericContextMenu`
3. Update `GlobalInteractionPlugin.cs` constructor to match
4. Update `DynamicWorldContainer.cs` to pass new deps to `GlobalInteractionPlugin`
5. Adapt `GenericUserProfileContextMenuController.cs` for `isOpenedOnWorldAvatar`
6. Adapt `CommunityInvitationContextMenuButtonHandler.cs` return type

**Option B: Cherry-pick + revert (more work)**
Cherry-pick individual commits, then manually revert social emote parts. Risky due to
heavy merge commits and interleaved changes.

---

## 5. DEPENDENCY GRAPH (context menu only)

```
ProcessOtherAvatarsInteractionSystem
  ├── IWeb3IdentityCache (already available in container)
  ├── ObjectProxy<Entity> cameraEntityProxy (already available)
  ├── Entity playerEntity (already available)
  ├── IMVCManagerMenusAccessFacade (already used)
  │   └── isOpenedOnWorldAvatar param (new)
  │       └── GenericUserProfileContextMenuController
  │           └── CommunityInvitationContextMenuButtonHandler (return type)
  ├── CursorState.LockedWithUI (new)
  │   ├── CursorComponent.cs
  │   ├── PointerLockIntention.cs (WithUI field)
  │   ├── UpdateCursorInputSystem.cs
  │   ├── ControlCinemachineVirtualCameraSystem.cs
  │   └── UpdateCameraInputSystem.cs
  └── dclInput.Player.RightPointer (right-click binding)
```

No new assemblies or asmdef references needed for context menu alone.

---

## 6. POST-EXTRACTION AUDIT (2026-03-31)

Перенос выполнен коммитом `8288007ff` ("migrated context menu from social emotes").
Ниже — результат покомпонентного сравнения ORIGINAL (PR) vs MY VERSION vs BASE (dev).

### 6.1 Файлы перенесены корректно ✅

| Файл | Статус |
|------|--------|
| `CursorComponent.cs` | Точная копия PR |
| `UpdateCameraInputSystem.cs` | Точная копия PR |
| `PointerLockIntention.cs` | Точная копия PR |
| `UpdateCursorInputSystem.cs` | Все 5 изменений PR перенесены |
| `IMVCManagerMenusAccessFacade.cs` | `isOpenedOnWorldAvatar` добавлен |
| `MVCManagerMenusAccessFacade.cs` | `isOpenedOnWorldAvatar` добавлен, наши proximity deps сохранены |
| `ProcessOtherAvatarsInteractionSystem.cs` | Намеренная переработка — social emotes убраны, правый клик + LockedWithUI + close-on-move сохранены |
| `GlobalInteractionPlugin.cs` | Намеренно: proximity deps сохранены, mvcManager заменён на cameraEntityProxy |
| `DynamicWorldContainer.cs` | Соответствует GlobalInteractionPlugin |
| `CommunityInvitationContextMenuButtonHandler.cs` | Return type изменён корректно |

### 6.2 Найденные баги 🐛

#### BUG 1 (CRITICAL) — Мерцание тултипа при наведении на аватар

**Файлы:** `HoverCanvasTooltip.uxml`, `HoverCanvasTooltipElement.cs`

**Проблема:** Эти файлы были checkout-нуты из PR целиком, но **не нужны** для нашей фичи.
Изменения PR vs dev:
- UXML: убран `picking-mode="Ignore"` со ВСЕХ элементов тултипа
- CS: `[UxmlElement] partial class` → `class` + `UxmlFactory`; `Image` → `VisualElement`

Без `picking-mode="Ignore"` тултип перехватывает pointer events, создавая feedback loop:
1. Frame N: тултипа нет → `IsPointerOverGameObject()` = false → аватар найден → тултип показан
2. Frame N+1: тултип pickable → `IsPointerOverGameObject()` = true → `canHover = false` → тултип убран
3. Repeat → мерцание каждый кадр

**Фикс:** `git checkout dev -- <оба файла>`

#### BUG 2 — Пропущена проверка LockedWithUI в ControlCinemachineVirtualCameraSystem

**Файл:** `ControlCinemachineVirtualCameraSystem.cs` → `HandleOffset()`

**Проблема:** Наш коммит убрал `|| cursorComponent.CursorState == CursorState.LockedWithUI`
из guard clause, который есть и в dev, и в PR. Камера продолжает двигать offset плечо
при открытом контекстном меню.

**Фикс:** Добавить обратно условие:
```csharp
if (cameraComponent.Mode is not (CameraMode.DroneView or CameraMode.ThirdPerson)
    || cursorComponent.CursorState == CursorState.LockedWithUI)
    return;
```

#### BUG 3 — Дубликаты AddControl в GenericUserProfileContextMenuController

**Файл:** `GenericUserProfileContextMenuController.cs`

**Проблема:** `contextMenuJumpInButton` и `contextMenuBlockUserButton` добавлены в контекстное
меню дважды. Также `contextMenuMentionButton` инициализирован с `true` вместо `false`.

**Фикс:** Убрать дублирующие `.AddControl()`, поправить init аргумент.

### 6.3 Намеренно не перенесённые изменения из PR

Все social emotes: `SocialEmoteInteractionsManager`, `ShowAvatarHighlightIntent`,
`OpenEmoteOutcomeContextMenu`, `OnOutcomePerformed`, `socialEmoteInteractionTooltip`,
`IWeb3IdentityCache` (не нужен без distance-check), `Entity playerEntity` (не нужен без
distance-check), `SocialEmoteOutcomesContextMenuSettings`, `SocialEmotesSettings`,
`AvatarHighlightSystem`, `contextMenuSocialEmoteButton`.
