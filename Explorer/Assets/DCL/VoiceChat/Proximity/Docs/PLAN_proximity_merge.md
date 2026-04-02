# Proximity Voice Chat — Merge Plan

## Обзор

Поэтапный перенос proximity voice chat из `feat/proximity-voice-chat` (клон-репо) в основной репозиторий. 4 PR, каждый — изолированный bounded context.

**Источник:** `C:\DCL\unity-explorer` ветка `feat/proximity-voice-chat`
**Не переносим:** Lip Sync (ProximityLipSyncSystem, ProximityLipSyncComponent, Mouth Atlas)

---

## Документация для чтения

Расположена в source: `Assets/DCL/VoiceChat/Proximity/Docs/`

| Документ | Когда читать |
|---|---|
| `ADR_proximity_voice_chat.md` | Перед PR 1 — архитектура, Island Room, shared dict bridge |
| `ADR_streaming_audioclip.md` | Перед PR 1 — ручная спатиализация (ITD/ILD/HRTF) |
| `ADR_lazy_track_publishing.md` | Перед PR 1 — lazy publish при первом Speaking |
| `ANALYSIS_voicechat_features.md` | Общий обзор фич |
| `MutePersistence/` | Перед PR 2 — REST API мьют-персистенция |
| `AudioEffectsZone/ADR_audio_effect_zones.md` | Перед PR 4 — зоны аудио-эффектов |

---

## PR 1: Core Audio Tracks — Публикация и слушание

**Bounded context:** Игроки автоматически публикуют аудио и слышат друг друга в Island Room с 3D spatial audio. Координация с Private/Community calls (suppress/resume).

**Ветка:** `feat/nearby-voice-chat/audio-tracks-core`

**Что входит:**
- LiveKit SDK → ветка `feat/mono-spatial-audio` (spatial DSP)
- `ProximityVoiceChatStateModel` — state machine (стартует в Speaking для auto-publish)
- `VoiceChatConfiguration` — proximity audio поля, spatial settings, ITD/ILD/HRTF
- `ProximityAudioSourceComponent` — ECS компонент
- `ProximityPanCalculator` — 3D angle → LiveKit spatial DSP
- `ProximityAudioPositionSystem` — ECS система позиционирования
- `ProximityVoiceChatManager` — ядро: publish/subscribe, suppress/resume (без mute)
- `ProximityAudioDebugWidget` — runtime debug слайдеры
- Audio Mixer update (ProximityChatAudioMixerGroup)

**Архитектурное выравнивание (микро 6):**
- `VoiceChatDisconnectReasonHelper` проверка в `OnConnectionUpdated` (как в RoomManager)
- `Owned/Weak` паттерн для микрофонного трека (как в TrackManager)
- `VoiceChatTrackPublishHelper` — общий static helper для publish логики (убирает ~85% дублирования между TrackManager и ProximityManager)

**Что НЕ входит:** Mute service, Nametags, Sidebar UI, AudioEffect Zones, LipSync

**Детальный план:** [PLAN_PR1_core_audio_tracks.md](PLAN_PR1_core_audio_tracks.md)

---

## PR 2: Nametag и Context Menu

**Bounded context:** Визуальная индикация кто говорит на nametags + per-player mute через контекст-меню.

**Что входит:**
- `ActiveSpeakersDiffTracker` — отслеживание смены спикеров
- `ProximityNametagsHandler` — bridge IslandRoom.ActiveSpeakers → VoiceChatNametagComponent
- `ProximityMuteService` (полная реализация) + `MutePersistence/` (REST API, cache)
- Обновление `ProximityVoiceChatManager` — добавление mute кода
- `VoiceChatPlugin` — identityCache, ActiveSpeakers подписка, ProximityNametagsHandler
- `NametagElement.cs` / `NametagStyle.uss` — стили speaking indicator
- `GenericUserProfileContextMenuController` — кнопка mute в контекст-меню

**Зависит от:** PR 1

---

## PR 3: Sidebar UI

**Bounded context:** Кнопка proximity voice в сайдбаре + панель управления (громкость, hear toggle, speak).

**Что входит:**
- `ProximityVoiceChatButtonController` + `ProximityVoiceChatButtonView` (sidebar button)
- `NearbyVoiceWidgetController` + `NearbyVoiceWidgetView` (side panel)
- `NearbyVoicePanelController`
- UI prefabs и текстуры
- `VoiceChatPlugin` — UI controller wiring, view injection
- State model возврат к `Hearing` по умолчанию (UI управляет Speaking)

**Зависит от:** PR 1, PR 2

---

## PR 4: AudioEffect Zones

**Bounded context:** SDK-компонент для сцен: зоны с аудио-эффектами (тишина, реверб, эхо, фильтры).

**Что входит:**
- `AudioEffectZone.gen.cs` — protobuf-generated SDK компонент
- `AudioEffectZoneHandlerSystem` — ECS система обработки зон
- `SDKEntityTriggerArea` интеграция — коллайдер-based определение попадания аватаров
- Audio filter компоненты (ReverbFilter, EchoFilter, LowPassFilter)

**Зависит от:** PR 1
