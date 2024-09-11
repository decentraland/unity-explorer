
import * as t from "ts-interface-checker";
import { ITypeSuite } from "ts-interface-checker";
// tslint:disable:object-literal-key-quotes

export const VideoTrackSourceType = t.enumtype({
  "VTST_UNKNOWN": 0,
  "VTST_CAMERA": 1,
  "VTST_SCREEN_SHARE": 2,
  "UNRECOGNIZED": -1,
});

export const VideoTracksActiveStreamsRequest = t.iface([], {
});

export const VideoTracksActiveStreamsResponse = t.iface([], {
  "streams": t.array("VideoTracksActiveStreamsData"),
});

export const VideoTracksActiveStreamsData = t.iface([], {
  "identity": "string",
  "trackSid": "string",
  "sourceType": "VideoTrackSourceType",
});

export const RealSendRequest = t.iface([], {
  "message": "string",
});

export const RealSendResponse = t.iface([], {
});

export const SendBinaryRequest = t.iface([], {
  "data": t.array("Uint8Array"),
});

export const SendBinaryResponse = t.iface([], {
  "data": t.array("Uint8Array"),
});

export const Position = t.iface([], {
  "x": "number",
  "y": "number",
  "z": "number",
});

export const Vector3 = t.iface([], {
  "x": "number",
  "y": "number",
  "z": "number",
});

export const Vector2 = t.iface([], {
  "x": "number",
  "y": "number",
});

export const Quaternion = t.iface([], {
  "x": "number",
  "y": "number",
  "z": "number",
  "w": "number",
});

export const Color3 = t.iface([], {
  "r": "number",
  "g": "number",
  "b": "number",
});

export const Color4 = t.iface([], {
  "r": "number",
  "g": "number",
  "b": "number",
  "a": "number",
});

export const ECS6Color4 = t.iface([], {
  "r": "number",
  "g": "number",
  "b": "number",
  "a": t.opt(t.union("number", "undefined")),
});

export const Area = t.iface([], {
  "box": t.opt(t.union("Vector3", "undefined")),
});

export const UiValue = t.iface([], {
  "value": t.opt(t.union("number", "undefined")),
  "type": t.opt(t.union("UiValue_UiValueType", "undefined")),
});

export const UiValue_UiValueType = t.enumtype({
  "UVT_PERCENT": 0,
  "PIXELS": 1,
  "UNRECOGNIZED": -1,
});

export const ECS6ComponentAvatarModifierArea = t.iface([], {
  "area": t.opt(t.union("Area", "undefined")),
  "modifiers": t.array("string"),
  "excludeIds": t.array("string"),
});

export const ECS6ComponentTransform = t.iface([], {
  "position": t.opt(t.union("Vector3", "undefined")),
  "rotation": t.opt(t.union("Quaternion", "undefined")),
  "scale": t.opt(t.union("Vector3", "undefined")),
});

export const ECS6ComponentAttachToAvatar = t.iface([], {
  "avatarId": t.opt(t.union("string", "undefined")),
  "anchorPointId": t.opt(t.union("ECS6ComponentAttachToAvatar_AttachToAvatarAnchorPointId", "undefined")),
  "avatarSceneId": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentAttachToAvatar_AttachToAvatarAnchorPointId = t.enumtype({
  "ATAAP_POSITION": 0,
  "ATAAP_NAME_TAG": 1,
  "ATAAP_LEFT_HAND": 2,
  "ATAAP_RIGHT_HAND": 3,
  "UNRECOGNIZED": -1,
});

export const ECS6ComponentBillboard = t.iface([], {
  "x": t.opt(t.union("boolean", "undefined")),
  "y": t.opt(t.union("boolean", "undefined")),
  "z": t.opt(t.union("boolean", "undefined")),
});

export const ECS6ComponentBoxShape = t.iface([], {
  "withCollisions": t.opt(t.union("boolean", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "uvs": t.array("number"),
});

export const ECS6ComponentSphereShape = t.iface([], {
  "withCollisions": t.opt(t.union("boolean", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
});

export const ECS6ComponentCircleShape = t.iface([], {
  "withCollisions": t.opt(t.union("boolean", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "segments": t.opt(t.union("number", "undefined")),
  "arc": t.opt(t.union("number", "undefined")),
});

export const ECS6ComponentPlaneShape = t.iface([], {
  "withCollisions": t.opt(t.union("boolean", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "uvs": t.array("number"),
  "width": t.opt(t.union("number", "undefined")),
  "height": t.opt(t.union("number", "undefined")),
});

export const ECS6ComponentConeShape = t.iface([], {
  "withCollisions": t.opt(t.union("boolean", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "radiusTop": t.opt(t.union("number", "undefined")),
  "radiusBottom": t.opt(t.union("number", "undefined")),
  "segmentsHeight": t.opt(t.union("number", "undefined")),
  "segmentsRadial": t.opt(t.union("number", "undefined")),
  "openEnded": t.opt(t.union("boolean", "undefined")),
  "radius": t.opt(t.union("number", "undefined")),
  "arc": t.opt(t.union("number", "undefined")),
});

export const ECS6ComponentCylinderShape = t.iface([], {
  "withCollisions": t.opt(t.union("boolean", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "radiusTop": t.opt(t.union("number", "undefined")),
  "radiusBottom": t.opt(t.union("number", "undefined")),
  "segmentsHeight": t.opt(t.union("number", "undefined")),
  "segmentsRadial": t.opt(t.union("number", "undefined")),
  "openEnded": t.opt(t.union("boolean", "undefined")),
  "radius": t.opt(t.union("number", "undefined")),
  "arc": t.opt(t.union("number", "undefined")),
});

export const ECS6ComponentGltfShape = t.iface([], {
  "withCollisions": t.opt(t.union("boolean", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "src": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentNftShape = t.iface([], {
  "withCollisions": t.opt(t.union("boolean", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "src": t.opt(t.union("string", "undefined")),
  "style": t.opt(t.union("ECS6ComponentNftShape_PictureFrameStyle", "undefined")),
  "color": t.opt(t.union("Color3", "undefined")),
});

export const ECS6ComponentNftShape_PictureFrameStyle = t.enumtype({
  "PFS_CLASSIC": 0,
  "PFS_BAROQUE_ORNAMENT": 1,
  "PFS_DIAMOND_ORNAMENT": 2,
  "PFS_MINIMAL_WIDE": 3,
  "PFS_MINIMAL_GREY": 4,
  "PFS_BLOCKY": 5,
  "PFS_GOLD_EDGES": 6,
  "PFS_GOLD_CARVED": 7,
  "PFS_GOLD_WIDE": 8,
  "PFS_GOLD_ROUNDED": 9,
  "PFS_METAL_MEDIUM": 10,
  "PFS_METAL_WIDE": 11,
  "PFS_METAL_SLIM": 12,
  "PFS_METAL_ROUNDED": 13,
  "PFS_PINS": 14,
  "PFS_MINIMAL_BLACK": 15,
  "PFS_MINIMAL_WHITE": 16,
  "PFS_TAPE": 17,
  "PFS_WOOD_SLIM": 18,
  "PFS_WOOD_WIDE": 19,
  "PFS_WOOD_TWIGS": 20,
  "PFS_CANVAS": 21,
  "PFS_NONE": 22,
  "UNRECOGNIZED": -1,
});

export const ECS6ComponentTexture = t.iface([], {
  "src": t.opt(t.union("string", "undefined")),
  "samplingMode": t.opt(t.union("number", "undefined")),
  "wrap": t.opt(t.union("number", "undefined")),
  "hasAlpha": t.opt(t.union("boolean", "undefined")),
});

export const ECS6ComponentAnimator = t.iface([], {
  "states": t.array("ECS6ComponentAnimator_AnimationState"),
});

export const ECS6ComponentAnimator_AnimationState = t.iface([], {
  "clip": t.opt(t.union("string", "undefined")),
  "looping": t.opt(t.union("boolean", "undefined")),
  "weight": t.opt(t.union("number", "undefined")),
  "playing": t.opt(t.union("boolean", "undefined")),
  "shouldReset": t.opt(t.union("boolean", "undefined")),
  "speed": t.opt(t.union("number", "undefined")),
  "name": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentObjShape = t.iface([], {
  "withCollisions": t.opt(t.union("boolean", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "src": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentFont = t.iface([], {
  "src": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentTextShape = t.iface([], {
  "outlineWidth": t.opt(t.union("number", "undefined")),
  "outlineColor": t.opt(t.union("Color3", "undefined")),
  "color": t.opt(t.union("Color3", "undefined")),
  "fontSize": t.opt(t.union("number", "undefined")),
  "font": t.opt(t.union("string", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "value": t.opt(t.union("string", "undefined")),
  "lineSpacing": t.opt(t.union("string", "undefined")),
  "lineCount": t.opt(t.union("number", "undefined")),
  "textWrapping": t.opt(t.union("boolean", "undefined")),
  "shadowBlur": t.opt(t.union("number", "undefined")),
  "shadowOffsetX": t.opt(t.union("number", "undefined")),
  "shadowOffsetY": t.opt(t.union("number", "undefined")),
  "shadowColor": t.opt(t.union("Color3", "undefined")),
  "hTextAlign": t.opt(t.union("string", "undefined")),
  "vTextAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("number", "undefined")),
  "height": t.opt(t.union("number", "undefined")),
  "paddingTop": t.opt(t.union("number", "undefined")),
  "paddingRight": t.opt(t.union("number", "undefined")),
  "paddingBottom": t.opt(t.union("number", "undefined")),
  "paddingLeft": t.opt(t.union("number", "undefined")),
  "billboard": t.opt(t.union("boolean", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
});

export const ECS6ComponentMaterial = t.iface([], {
  "alphaTest": t.opt(t.union("number", "undefined")),
  "albedoColor": t.opt(t.union("ECS6Color4", "undefined")),
  "emissiveColor": t.opt(t.union("Color3", "undefined")),
  "metallic": t.opt(t.union("number", "undefined")),
  "roughness": t.opt(t.union("number", "undefined")),
  "reflectivityColor": t.opt(t.union("Color3", "undefined")),
  "directIntensity": t.opt(t.union("number", "undefined")),
  "microSurface": t.opt(t.union("number", "undefined")),
  "emissiveIntensity": t.opt(t.union("number", "undefined")),
  "specularIntensity": t.opt(t.union("number", "undefined")),
  "albedoTexture": t.opt(t.union("string", "undefined")),
  "alphaTexture": t.opt(t.union("string", "undefined")),
  "emissiveTexture": t.opt(t.union("string", "undefined")),
  "bumpTexture": t.opt(t.union("string", "undefined")),
  "transparencyMode": t.opt(t.union("number", "undefined")),
  "castShadows": t.opt(t.union("boolean", "undefined")),
});

export const ECS6ComponentBasicMaterial = t.iface([], {
  "alphaTest": t.opt(t.union("number", "undefined")),
  "texture": t.opt(t.union("string", "undefined")),
  "castShadows": t.opt(t.union("boolean", "undefined")),
});

export const ECS6ComponentUuidCallback = t.iface([], {
  "button": t.opt(t.union("string", "undefined")),
  "hoverText": t.opt(t.union("string", "undefined")),
  "distance": t.opt(t.union("number", "undefined")),
  "showFeedback": t.opt(t.union("boolean", "undefined")),
  "type": t.opt(t.union("string", "undefined")),
  "uuid": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentSmartItem = t.iface([], {
});

export const ECS6ComponentVideoClip = t.iface([], {
  "url": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentVideoTexture = t.iface([], {
  "samplingMode": t.opt(t.union("number", "undefined")),
  "wrap": t.opt(t.union("number", "undefined")),
  "volume": t.opt(t.union("number", "undefined")),
  "playbackRate": t.opt(t.union("number", "undefined")),
  "seek": t.opt(t.union("number", "undefined")),
  "playing": t.opt(t.union("boolean", "undefined")),
  "loop": t.opt(t.union("boolean", "undefined")),
  "videoClipId": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentVideoTexture_VideoStatus = t.enumtype({
  "NONE": 0,
  "ERROR": 1,
  "LOADING": 2,
  "READY": 3,
  "PLAYING": 4,
  "BUFFERING": 5,
  "UNRECOGNIZED": -1,
});

export const ECS6ComponentCameraModeArea = t.iface([], {
  "area": t.union("Area", "undefined"),
  "cameraMode": "ECS6ComponentCameraModeArea_CameraMode",
});

export const ECS6ComponentCameraModeArea_CameraMode = t.enumtype({
  "CM_FIRST_PERSON": 0,
  "CM_THIRD_PERSON": 1,
  "CM_BUILDING_TOOL_GOD_MODE": 2,
  "UNRECOGNIZED": -1,
});

export const ECS6ComponentAvatarTexture = t.iface([], {
  "samplingMode": t.opt(t.union("number", "undefined")),
  "wrap": t.opt(t.union("number", "undefined")),
  "hasAlpha": t.opt(t.union("boolean", "undefined")),
  "userId": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentAudioClip = t.iface([], {
  "url": t.opt(t.union("string", "undefined")),
  "loop": t.opt(t.union("boolean", "undefined")),
  "loadingCompleteEventId": t.opt(t.union("string", "undefined")),
  "volume": t.opt(t.union("number", "undefined")),
});

export const ECS6ComponentAudioSource = t.iface([], {
  "audioClipId": t.opt(t.union("string", "undefined")),
  "loop": t.opt(t.union("boolean", "undefined")),
  "volume": t.opt(t.union("number", "undefined")),
  "playing": t.opt(t.union("boolean", "undefined")),
  "pitch": t.opt(t.union("number", "undefined")),
  "playedAtTimestamp": t.opt(t.union("number", "undefined")),
});

export const ECS6ComponentAudioStream = t.iface([], {
  "url": t.opt(t.union("string", "undefined")),
  "playing": t.opt(t.union("boolean", "undefined")),
  "volume": t.opt(t.union("number", "undefined")),
});

export const ECS6ComponentAvatarShape = t.iface([], {
  "id": t.opt(t.union("string", "undefined")),
  "name": t.opt(t.union("string", "undefined")),
  "expressionTriggerId": t.opt(t.union("string", "undefined")),
  "expressionTriggerTimestamp": t.opt(t.union("number", "undefined")),
  "bodyShape": t.opt(t.union("string", "undefined")),
  "wearables": t.array("string"),
  "emotes": t.array("ECS6ComponentAvatarShape_Emote"),
  "skinColor": t.opt(t.union("ECS6Color4", "undefined")),
  "hairColor": t.opt(t.union("ECS6Color4", "undefined")),
  "eyeColor": t.opt(t.union("ECS6Color4", "undefined")),
  "useDummyModel": t.opt(t.union("boolean", "undefined")),
  "talking": t.opt(t.union("boolean", "undefined")),
});

export const ECS6ComponentAvatarShape_Emote = t.iface([], {
  "slot": t.opt(t.union("number", "undefined")),
  "urn": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentGizmos = t.iface([], {
  "position": t.opt(t.union("boolean", "undefined")),
  "rotation": t.opt(t.union("boolean", "undefined")),
  "scale": t.opt(t.union("boolean", "undefined")),
  "cycle": t.opt(t.union("boolean", "undefined")),
  "selectedGizmo": t.opt(t.union("string", "undefined")),
  "localReference": t.opt(t.union("boolean", "undefined")),
});

export const ECS6ComponentUiShape = t.iface([], {
  "name": t.opt(t.union("string", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "hAlign": t.opt(t.union("string", "undefined")),
  "vAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("UiValue", "undefined")),
  "height": t.opt(t.union("UiValue", "undefined")),
  "positionX": t.opt(t.union("UiValue", "undefined")),
  "positionY": t.opt(t.union("UiValue", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "parentComponent": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentUiContainerRect = t.iface([], {
  "name": t.opt(t.union("string", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "hAlign": t.opt(t.union("string", "undefined")),
  "vAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("UiValue", "undefined")),
  "height": t.opt(t.union("UiValue", "undefined")),
  "positionX": t.opt(t.union("UiValue", "undefined")),
  "positionY": t.opt(t.union("UiValue", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "parentComponent": t.opt(t.union("string", "undefined")),
  "thickness": t.opt(t.union("number", "undefined")),
  "color": t.opt(t.union("ECS6Color4", "undefined")),
  "alignmentUsesSize": t.opt(t.union("boolean", "undefined")),
});

export const ECS6ComponentUiContainerStack = t.iface([], {
  "name": t.opt(t.union("string", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "hAlign": t.opt(t.union("string", "undefined")),
  "vAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("UiValue", "undefined")),
  "height": t.opt(t.union("UiValue", "undefined")),
  "positionX": t.opt(t.union("UiValue", "undefined")),
  "positionY": t.opt(t.union("UiValue", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "parentComponent": t.opt(t.union("string", "undefined")),
  "adaptWidth": t.opt(t.union("boolean", "undefined")),
  "adaptHeight": t.opt(t.union("boolean", "undefined")),
  "color": t.opt(t.union("ECS6Color4", "undefined")),
  "stackOrientation": t.opt(t.union("ECS6ComponentUiContainerStack_UIStackOrientation", "undefined")),
  "spacing": t.opt(t.union("number", "undefined")),
});

export const ECS6ComponentUiContainerStack_UIStackOrientation = t.enumtype({
  "VERTICAL": 0,
  "HORIZONTAL": 1,
  "UNRECOGNIZED": -1,
});

export const ECS6ComponentUiButton = t.iface([], {
  "name": t.opt(t.union("string", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "hAlign": t.opt(t.union("string", "undefined")),
  "vAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("UiValue", "undefined")),
  "height": t.opt(t.union("UiValue", "undefined")),
  "positionX": t.opt(t.union("UiValue", "undefined")),
  "positionY": t.opt(t.union("UiValue", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "parentComponent": t.opt(t.union("string", "undefined")),
  "fontSize": t.opt(t.union("number", "undefined")),
  "fontWeight": t.opt(t.union("string", "undefined")),
  "thickness": t.opt(t.union("number", "undefined")),
  "cornerRadius": t.opt(t.union("number", "undefined")),
  "color": t.opt(t.union("ECS6Color4", "undefined")),
  "background": t.opt(t.union("ECS6Color4", "undefined")),
  "paddingTop": t.opt(t.union("number", "undefined")),
  "paddingRight": t.opt(t.union("number", "undefined")),
  "paddingBottom": t.opt(t.union("number", "undefined")),
  "paddingLeft": t.opt(t.union("number", "undefined")),
  "shadowBlur": t.opt(t.union("number", "undefined")),
  "shadowOffsetX": t.opt(t.union("number", "undefined")),
  "shadowOffsetY": t.opt(t.union("number", "undefined")),
  "shadowColor": t.opt(t.union("ECS6Color4", "undefined")),
  "text": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentUiText = t.iface([], {
  "name": t.opt(t.union("string", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "hAlign": t.opt(t.union("string", "undefined")),
  "vAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("UiValue", "undefined")),
  "height": t.opt(t.union("UiValue", "undefined")),
  "positionX": t.opt(t.union("UiValue", "undefined")),
  "positionY": t.opt(t.union("UiValue", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "parentComponent": t.opt(t.union("string", "undefined")),
  "outlineWidth": t.opt(t.union("number", "undefined")),
  "outlineColor": t.opt(t.union("ECS6Color4", "undefined")),
  "color": t.opt(t.union("ECS6Color4", "undefined")),
  "fontSize": t.opt(t.union("number", "undefined")),
  "fontAutoSize": t.opt(t.union("boolean", "undefined")),
  "font": t.opt(t.union("string", "undefined")),
  "value": t.opt(t.union("string", "undefined")),
  "lineSpacing": t.opt(t.union("number", "undefined")),
  "lineCount": t.opt(t.union("number", "undefined")),
  "adaptWidth": t.opt(t.union("boolean", "undefined")),
  "adaptHeight": t.opt(t.union("boolean", "undefined")),
  "textWrapping": t.opt(t.union("boolean", "undefined")),
  "shadowBlur": t.opt(t.union("number", "undefined")),
  "shadowOffsetX": t.opt(t.union("number", "undefined")),
  "shadowOffsetY": t.opt(t.union("number", "undefined")),
  "shadowColor": t.opt(t.union("ECS6Color4", "undefined")),
  "hTextAlign": t.opt(t.union("string", "undefined")),
  "vTextAlign": t.opt(t.union("string", "undefined")),
  "paddingTop": t.opt(t.union("number", "undefined")),
  "paddingRight": t.opt(t.union("number", "undefined")),
  "paddingBottom": t.opt(t.union("number", "undefined")),
  "paddingLeft": t.opt(t.union("number", "undefined")),
});

export const ECS6ComponentUiInputText = t.iface([], {
  "name": t.opt(t.union("string", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "hAlign": t.opt(t.union("string", "undefined")),
  "vAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("UiValue", "undefined")),
  "height": t.opt(t.union("UiValue", "undefined")),
  "positionX": t.opt(t.union("UiValue", "undefined")),
  "positionY": t.opt(t.union("UiValue", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "parentComponent": t.opt(t.union("string", "undefined")),
  "outlineWidth": t.opt(t.union("number", "undefined")),
  "outlineColor": t.opt(t.union("ECS6Color4", "undefined")),
  "color": t.opt(t.union("ECS6Color4", "undefined")),
  "fontSize": t.opt(t.union("number", "undefined")),
  "font": t.opt(t.union("string", "undefined")),
  "value": t.opt(t.union("string", "undefined")),
  "placeholder": t.opt(t.union("string", "undefined")),
  "margin": t.opt(t.union("number", "undefined")),
  "focusedBackground": t.opt(t.union("ECS6Color4", "undefined")),
  "textWrapping": t.opt(t.union("boolean", "undefined")),
  "shadowBlur": t.opt(t.union("number", "undefined")),
  "shadowOffsetX": t.opt(t.union("number", "undefined")),
  "shadowOffsetY": t.opt(t.union("number", "undefined")),
  "shadowColor": t.opt(t.union("ECS6Color4", "undefined")),
  "hTextAlign": t.opt(t.union("string", "undefined")),
  "vTextAlign": t.opt(t.union("string", "undefined")),
  "paddingTop": t.opt(t.union("number", "undefined")),
  "paddingRight": t.opt(t.union("number", "undefined")),
  "paddingBottom": t.opt(t.union("number", "undefined")),
  "paddingLeft": t.opt(t.union("number", "undefined")),
  "onTextChanged": t.opt(t.union("string", "undefined")),
  "onFocus": t.opt(t.union("string", "undefined")),
  "onBlur": t.opt(t.union("string", "undefined")),
  "onTextSubmit": t.opt(t.union("string", "undefined")),
  "onChanged": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentUiImage = t.iface([], {
  "name": t.opt(t.union("string", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "hAlign": t.opt(t.union("string", "undefined")),
  "vAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("UiValue", "undefined")),
  "height": t.opt(t.union("UiValue", "undefined")),
  "positionX": t.opt(t.union("UiValue", "undefined")),
  "positionY": t.opt(t.union("UiValue", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "parentComponent": t.opt(t.union("string", "undefined")),
  "sourceLeft": t.opt(t.union("number", "undefined")),
  "sourceTop": t.opt(t.union("number", "undefined")),
  "sourceWidth": t.opt(t.union("number", "undefined")),
  "sourceHeight": t.opt(t.union("number", "undefined")),
  "source": t.opt(t.union("string", "undefined")),
  "paddingTop": t.opt(t.union("number", "undefined")),
  "paddingRight": t.opt(t.union("number", "undefined")),
  "paddingBottom": t.opt(t.union("number", "undefined")),
  "paddingLeft": t.opt(t.union("number", "undefined")),
  "sizeInPixels": t.opt(t.union("boolean", "undefined")),
  "onClick": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentUiScrollRect = t.iface([], {
  "name": t.opt(t.union("string", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "hAlign": t.opt(t.union("string", "undefined")),
  "vAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("UiValue", "undefined")),
  "height": t.opt(t.union("UiValue", "undefined")),
  "positionX": t.opt(t.union("UiValue", "undefined")),
  "positionY": t.opt(t.union("UiValue", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "parentComponent": t.opt(t.union("string", "undefined")),
  "valueX": t.opt(t.union("number", "undefined")),
  "valueY": t.opt(t.union("number", "undefined")),
  "backgroundColor": t.opt(t.union("ECS6Color4", "undefined")),
  "isHorizontal": t.opt(t.union("boolean", "undefined")),
  "isVertical": t.opt(t.union("boolean", "undefined")),
  "paddingTop": t.opt(t.union("number", "undefined")),
  "paddingRight": t.opt(t.union("number", "undefined")),
  "paddingBottom": t.opt(t.union("number", "undefined")),
  "paddingLeft": t.opt(t.union("number", "undefined")),
  "onChanged": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentUiWorldSpaceShape = t.iface([], {
  "name": t.opt(t.union("string", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "hAlign": t.opt(t.union("string", "undefined")),
  "vAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("UiValue", "undefined")),
  "height": t.opt(t.union("UiValue", "undefined")),
  "positionX": t.opt(t.union("UiValue", "undefined")),
  "positionY": t.opt(t.union("UiValue", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "parentComponent": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentUiScreenSpaceShape = t.iface([], {
  "name": t.opt(t.union("string", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "hAlign": t.opt(t.union("string", "undefined")),
  "vAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("UiValue", "undefined")),
  "height": t.opt(t.union("UiValue", "undefined")),
  "positionX": t.opt(t.union("UiValue", "undefined")),
  "positionY": t.opt(t.union("UiValue", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "parentComponent": t.opt(t.union("string", "undefined")),
});

export const ECS6ComponentUiFullScreenShape = t.iface([], {
  "name": t.opt(t.union("string", "undefined")),
  "visible": t.opt(t.union("boolean", "undefined")),
  "opacity": t.opt(t.union("number", "undefined")),
  "hAlign": t.opt(t.union("string", "undefined")),
  "vAlign": t.opt(t.union("string", "undefined")),
  "width": t.opt(t.union("UiValue", "undefined")),
  "height": t.opt(t.union("UiValue", "undefined")),
  "positionX": t.opt(t.union("UiValue", "undefined")),
  "positionY": t.opt(t.union("UiValue", "undefined")),
  "isPointerBlocker": t.opt(t.union("boolean", "undefined")),
  "parentComponent": t.opt(t.union("string", "undefined")),
});

export const OpenExternalUrlBody = t.iface([], {
  "url": "string",
});

export const OpenNFTDialogBody = t.iface([], {
  "assetContractAddress": "string",
  "tokenId": "string",
  "comment": t.opt(t.union("string", "undefined")),
});

export const ComponentBodyPayload = t.iface([], {
  "avatarModifierArea": t.opt(t.union("ECS6ComponentAvatarModifierArea", "undefined")),
  "transform": t.opt(t.union("ECS6ComponentTransform", "undefined")),
  "attachToAvatar": t.opt(t.union("ECS6ComponentAttachToAvatar", "undefined")),
  "billboard": t.opt(t.union("ECS6ComponentBillboard", "undefined")),
  "boxShape": t.opt(t.union("ECS6ComponentBoxShape", "undefined")),
  "sphereShape": t.opt(t.union("ECS6ComponentSphereShape", "undefined")),
  "circleShape": t.opt(t.union("ECS6ComponentCircleShape", "undefined")),
  "planeShape": t.opt(t.union("ECS6ComponentPlaneShape", "undefined")),
  "coneShape": t.opt(t.union("ECS6ComponentConeShape", "undefined")),
  "cylinderShape": t.opt(t.union("ECS6ComponentCylinderShape", "undefined")),
  "gltfShape": t.opt(t.union("ECS6ComponentGltfShape", "undefined")),
  "nftShape": t.opt(t.union("ECS6ComponentNftShape", "undefined")),
  "texture": t.opt(t.union("ECS6ComponentTexture", "undefined")),
  "animator": t.opt(t.union("ECS6ComponentAnimator", "undefined")),
  "objShape": t.opt(t.union("ECS6ComponentObjShape", "undefined")),
  "font": t.opt(t.union("ECS6ComponentFont", "undefined")),
  "textShape": t.opt(t.union("ECS6ComponentTextShape", "undefined")),
  "material": t.opt(t.union("ECS6ComponentMaterial", "undefined")),
  "basicMaterial": t.opt(t.union("ECS6ComponentBasicMaterial", "undefined")),
  "uuidCallback": t.opt(t.union("ECS6ComponentUuidCallback", "undefined")),
  "smartItem": t.opt(t.union("ECS6ComponentSmartItem", "undefined")),
  "videoClip": t.opt(t.union("ECS6ComponentVideoClip", "undefined")),
  "videoTexture": t.opt(t.union("ECS6ComponentVideoTexture", "undefined")),
  "cameraModeArea": t.opt(t.union("ECS6ComponentCameraModeArea", "undefined")),
  "avatarTexture": t.opt(t.union("ECS6ComponentAvatarTexture", "undefined")),
  "audioClip": t.opt(t.union("ECS6ComponentAudioClip", "undefined")),
  "audioSource": t.opt(t.union("ECS6ComponentAudioSource", "undefined")),
  "audioStream": t.opt(t.union("ECS6ComponentAudioStream", "undefined")),
  "avatarShape": t.opt(t.union("ECS6ComponentAvatarShape", "undefined")),
  "gizmos": t.opt(t.union("ECS6ComponentGizmos", "undefined")),
  "uiShape": t.opt(t.union("ECS6ComponentUiShape", "undefined")),
  "uiContainerRect": t.opt(t.union("ECS6ComponentUiContainerRect", "undefined")),
  "uiContainerStack": t.opt(t.union("ECS6ComponentUiContainerStack", "undefined")),
  "uiButton": t.opt(t.union("ECS6ComponentUiButton", "undefined")),
  "uiText": t.opt(t.union("ECS6ComponentUiText", "undefined")),
  "uiInputText": t.opt(t.union("ECS6ComponentUiInputText", "undefined")),
  "uiImage": t.opt(t.union("ECS6ComponentUiImage", "undefined")),
  "uiScrollRect": t.opt(t.union("ECS6ComponentUiScrollRect", "undefined")),
  "uiWorldSpaceShape": t.opt(t.union("ECS6ComponentUiWorldSpaceShape", "undefined")),
  "uiScreenSpaceShape": t.opt(t.union("ECS6ComponentUiScreenSpaceShape", "undefined")),
  "uiFullScreenShape": t.opt(t.union("ECS6ComponentUiFullScreenShape", "undefined")),
});

export const CreateEntityBody = t.iface([], {
  "id": "string",
});

export const RemoveEntityBody = t.iface([], {
  "id": "string",
});

export const UpdateEntityComponentBody = t.iface([], {
  "entityId": "string",
  "classId": "number",
  "name": "string",
  "componentData": t.union("ComponentBodyPayload", "undefined"),
});

export const AttachEntityComponentBody = t.iface([], {
  "entityId": "string",
  "name": "string",
  "id": "string",
});

export const ComponentRemovedBody = t.iface([], {
  "entityId": "string",
  "name": "string",
});

export const SetEntityParentBody = t.iface([], {
  "entityId": "string",
  "parentId": "string",
});

export const QueryBody = t.iface([], {
  "queryId": "string",
  "payload": t.union("QueryBody_RayQuery", "undefined"),
});

export const QueryBody_Ray = t.iface([], {
  "origin": t.union("Vector3", "undefined"),
  "direction": t.union("Vector3", "undefined"),
  "distance": "number",
});

export const QueryBody_RayQuery = t.iface([], {
  "queryId": "string",
  "queryType": "string",
  "ray": t.union("QueryBody_Ray", "undefined"),
});

export const ComponentCreatedBody = t.iface([], {
  "id": "string",
  "classId": "number",
  "name": "string",
});

export const ComponentDisposedBody = t.iface([], {
  "id": "string",
});

export const ComponentUpdatedBody = t.iface([], {
  "id": "string",
  "componentData": t.union("ComponentBodyPayload", "undefined"),
});

export const InitMessagesFinishedBody = t.iface([], {
});

export const EntityActionPayload = t.iface([], {
  "openExternalUrl": t.opt(t.union("OpenExternalUrlBody", "undefined")),
  "openNftDialog": t.opt(t.union("OpenNFTDialogBody", "undefined")),
  "createEntity": t.opt(t.union("CreateEntityBody", "undefined")),
  "removeEntity": t.opt(t.union("RemoveEntityBody", "undefined")),
  "updateEntityComponent": t.opt(t.union("UpdateEntityComponentBody", "undefined")),
  "attachEntityComponent": t.opt(t.union("AttachEntityComponentBody", "undefined")),
  "componentRemoved": t.opt(t.union("ComponentRemovedBody", "undefined")),
  "setEntityParent": t.opt(t.union("SetEntityParentBody", "undefined")),
  "query": t.opt(t.union("QueryBody", "undefined")),
  "componentCreated": t.opt(t.union("ComponentCreatedBody", "undefined")),
  "componentDisposed": t.opt(t.union("ComponentDisposedBody", "undefined")),
  "componentUpdated": t.opt(t.union("ComponentUpdatedBody", "undefined")),
  "initMessagesFinished": t.opt(t.union("InitMessagesFinishedBody", "undefined")),
});

export const EntityAction = t.iface([], {
  "tag": t.opt(t.union("string", "undefined")),
  "payload": t.union("EntityActionPayload", "undefined"),
});

export const EventDataType = t.enumtype({
  "EDT_GENERIC": 0,
  "EDT_POSITION_CHANGED": 1,
  "EDT_ROTATION_CHANGED": 2,
  "UNRECOGNIZED": -1,
});

export const ManyEntityAction = t.iface([], {
  "actions": t.array("EntityAction"),
});

export const SendBatchResponse = t.iface([], {
  "events": t.array("EventData"),
});

export const UnsubscribeRequest = t.iface([], {
  "eventId": "string",
});

export const SubscribeRequest = t.iface([], {
  "eventId": "string",
});

export const SubscribeResponse = t.iface([], {
});

export const UnsubscribeResponse = t.iface([], {
});

export const GenericPayload = t.iface([], {
  "eventId": "string",
  "eventData": "string",
});

export const ReadOnlyVector3 = t.iface([], {
  "x": "number",
  "y": "number",
  "z": "number",
});

export const ReadOnlyQuaternion = t.iface([], {
  "x": "number",
  "y": "number",
  "z": "number",
  "w": "number",
});

export const PositionChangedPayload = t.iface([], {
  "position": t.union("ReadOnlyVector3", "undefined"),
  "cameraPosition": t.union("ReadOnlyVector3", "undefined"),
  "playerHeight": "number",
});

export const RotationChangedPayload = t.iface([], {
  "rotation": t.union("ReadOnlyVector3", "undefined"),
  "quaternion": t.union("ReadOnlyQuaternion", "undefined"),
});

export const EventData = t.iface([], {
  "type": "EventDataType",
  "generic": t.opt(t.union("GenericPayload", "undefined")),
  "positionChanged": t.opt(t.union("PositionChangedPayload", "undefined")),
  "rotationChanged": t.opt(t.union("RotationChangedPayload", "undefined")),
});

export const CrdtSendToRendererRequest = t.iface([], {
  "data": "Uint8Array",
});

export const CrdtSendToResponse = t.iface([], {
  "data": t.array("Uint8Array"),
});

export const CrdtGetStateRequest = t.iface([], {
});

export const CrdtGetStateResponse = t.iface([], {
  "hasEntities": "boolean",
  "data": t.array("Uint8Array"),
});

export const CrdtMessageFromRendererRequest = t.iface([], {
});

export const CrdtMessageFromRendererResponse = t.iface([], {
  "data": t.array("Uint8Array"),
});

export const IsServerRequest = t.iface([], {
});

export const IsServerResponse = t.iface([], {
  "isServer": "boolean",
});

export const ContentMapping = t.iface([], {
  "file": "string",
  "hash": "string",
});

export const MinimalRunnableEntity = t.iface([], {
  "content": t.array("ContentMapping"),
  "metadataJson": "string",
});

export const BootstrapDataResponse = t.iface([], {
  "id": "string",
  "baseUrl": "string",
  "entity": t.union("MinimalRunnableEntity", "undefined"),
  "useFPSThrottling": "boolean",
});

export const PreviewModeResponse = t.iface([], {
  "isPreview": "boolean",
});

export const AreUnsafeRequestAllowedResponse = t.iface([], {
  "status": "boolean",
});

export const GetPlatformResponse = t.iface([], {
  "platform": "string",
});

export const EnvironmentRealm = t.iface([], {
  "domain": "string",
  "layer": "string",
  "room": "string",
  "serverName": "string",
  "displayName": "string",
  "protocol": "string",
});

export const GetCurrentRealmResponse = t.iface([], {
  "currentRealm": t.opt(t.union("EnvironmentRealm", "undefined")),
});

export const GetExplorerConfigurationResponse = t.iface([], {
  "clientUri": "string",
  "configurations": t.iface([], {
    [t.indexKey]: "string",
  }),
});

export const GetExplorerConfigurationResponse_ConfigurationsEntry = t.iface([], {
  "key": "string",
  "value": "string",
});

export const GetDecentralandTimeResponse = t.iface([], {
  "seconds": "number",
});

export const GetBootstrapDataRequest = t.iface([], {
});

export const IsPreviewModeRequest = t.iface([], {
});

export const GetPlatformRequest = t.iface([], {
});

export const AreUnsafeRequestAllowedRequest = t.iface([], {
});

export const GetCurrentRealmRequest = t.iface([], {
});

export const GetExplorerConfigurationRequest = t.iface([], {
});

export const GetDecentralandTimeRequest = t.iface([], {
});

export const RequirePaymentRequest = t.iface([], {
  "toAddress": "string",
  "amount": "number",
  "currency": "string",
});

export const RequirePaymentResponse = t.iface([], {
  "jsonAnyResponse": "string",
});

export const SignMessageRequest = t.iface([], {
  "message": t.iface([], {
    [t.indexKey]: "string",
  }),
});

export const SignMessageRequest_MessageEntry = t.iface([], {
  "key": "string",
  "value": "string",
});

export const SignMessageResponse = t.iface([], {
  "message": "string",
  "hexEncodedMessage": "string",
  "signature": "string",
});

export const ConvertMessageToObjectRequest = t.iface([], {
  "message": "string",
});

export const ConvertMessageToObjectResponse = t.iface([], {
  "dict": t.iface([], {
    [t.indexKey]: "string",
  }),
});

export const ConvertMessageToObjectResponse_DictEntry = t.iface([], {
  "key": "string",
  "value": "string",
});

export const SendAsyncRequest = t.iface([], {
  "id": "number",
  "method": "string",
  "jsonParams": "string",
});

export const SendAsyncResponse = t.iface([], {
  "jsonAnyResponse": "string",
});

export const GetUserAccountRequest = t.iface([], {
});

export const GetUserAccountResponse = t.iface([], {
  "address": t.opt(t.union("string", "undefined")),
});

export const Player = t.iface([], {
  "userId": "string",
});

export const PlayersGetUserDataResponse = t.iface([], {
  "data": t.opt(t.union("UserData", "undefined")),
});

export const PlayerListResponse = t.iface([], {
  "players": t.array("Player"),
});

export const GetPlayerDataRequest = t.iface([], {
  "userId": "string",
});

export const GetPlayersInSceneRequest = t.iface([], {
});

export const GetConnectedPlayersRequest = t.iface([], {
});

export const KillRequest = t.iface([], {
  "pid": "string",
});

export const KillResponse = t.iface([], {
  "status": "boolean",
});

export const SpawnRequest = t.iface([], {
  "pid": t.opt(t.union("string", "undefined")),
  "ens": t.opt(t.union("string", "undefined")),
});

export const SpawnResponse = t.iface([], {
  "pid": "string",
  "parentCid": "string",
  "name": "string",
  "ens": t.opt(t.union("string", "undefined")),
});

export const PxRequest = t.iface([], {
  "pid": "string",
});

export const GetPortableExperiencesLoadedRequest = t.iface([], {
});

export const GetPortableExperiencesLoadedResponse = t.iface([], {
  "loaded": t.array("SpawnResponse"),
});

export const ExitRequest = t.iface([], {
});

export const ExitResponse = t.iface([], {
  "status": "boolean",
});

export const MovePlayerToRequest = t.iface([], {
  "newRelativePosition": t.union("Vector3", "undefined"),
  "cameraTarget": t.opt(t.union("Vector3", "undefined")),
});

export const TeleportToRequest = t.iface([], {
  "worldCoordinates": t.union("Vector2", "undefined"),
});

export const TriggerEmoteRequest = t.iface([], {
  "predefinedEmote": "string",
});

export const ChangeRealmRequest = t.iface([], {
  "realm": "string",
  "message": t.opt(t.union("string", "undefined")),
});

export const OpenExternalUrlRequest = t.iface([], {
  "url": "string",
});

export const OpenNftDialogRequest = t.iface([], {
  "urn": "string",
});

export const UnblockPointerRequest = t.iface([], {
});

export const CommsAdapterRequest = t.iface([], {
  "connectionString": "string",
});

export const TriggerSceneEmoteRequest = t.iface([], {
  "src": "string",
  "loop": t.opt(t.union("boolean", "undefined")),
});

export const SuccessResponse = t.iface([], {
  "success": "boolean",
});

export const TriggerEmoteResponse = t.iface([], {
});

export const MovePlayerToResponse = t.iface([], {
});

export const TeleportToResponse = t.iface([], {
});

export const RealmInfo = t.iface([], {
  "baseUrl": "string",
  "realmName": "string",
  "networkId": "number",
  "commsAdapter": "string",
  "isPreview": "boolean",
});

export const GetRealmResponse = t.iface([], {
  "realmInfo": t.opt(t.union("RealmInfo", "undefined")),
});

export const GetWorldTimeResponse = t.iface([], {
  "seconds": "number",
});

export const GetRealmRequest = t.iface([], {
});

export const GetWorldTimeRequest = t.iface([], {
});

export const ReadFileRequest = t.iface([], {
  "fileName": "string",
});

export const ReadFileResponse = t.iface([], {
  "content": "Uint8Array",
  "hash": "string",
});

export const CurrentSceneEntityRequest = t.iface([], {
});

export const CurrentSceneEntityResponse = t.iface([], {
  "urn": "string",
  "content": t.array("ContentMapping"),
  "metadataJson": "string",
  "baseUrl": "string",
});

export const GetExplorerInformationRequest = t.iface([], {
});

export const GetExplorerInformationResponse = t.iface([], {
  "agent": "string",
  "platform": "string",
  "configurations": t.iface([], {
    [t.indexKey]: "string",
  }),
});

export const GetExplorerInformationResponse_ConfigurationsEntry = t.iface([], {
  "key": "string",
  "value": "string",
});

export const GetSceneRequest = t.iface([], {
});

export const GetSceneResponse = t.iface([], {
  "cid": "string",
  "metadata": "string",
  "baseUrl": "string",
  "contents": t.array("ContentMapping"),
});

export const FlatFetchInit = t.iface([], {
  "method": t.opt(t.union("string", "undefined")),
  "body": t.opt(t.union("string", "undefined")),
  "headers": t.iface([], {
    [t.indexKey]: "string",
  }),
});

export const FlatFetchInit_HeadersEntry = t.iface([], {
  "key": "string",
  "value": "string",
});

export const FlatFetchResponse = t.iface([], {
  "ok": "boolean",
  "status": "number",
  "statusText": "string",
  "headers": t.iface([], {
    [t.indexKey]: "string",
  }),
  "body": "string",
});

export const FlatFetchResponse_HeadersEntry = t.iface([], {
  "key": "string",
  "value": "string",
});

export const SignedFetchRequest = t.iface([], {
  "url": "string",
  "init": t.opt(t.union("FlatFetchInit", "undefined")),
});

export const GetHeadersResponse = t.iface([], {
  "headers": t.iface([], {
    [t.indexKey]: "string",
  }),
});

export const GetHeadersResponse_HeadersEntry = t.iface([], {
  "key": "string",
  "value": "string",
});

export const TestResult = t.iface([], {
  "name": "string",
  "ok": "boolean",
  "error": t.opt(t.union("string", "undefined")),
  "stack": t.opt(t.union("string", "undefined")),
  "totalFrames": "number",
  "totalTime": "number",
});

export const TestResultResponse = t.iface([], {
});

export const TestPlan = t.iface([], {
  "tests": t.array("TestPlan_TestPlanEntry"),
});

export const TestPlan_TestPlanEntry = t.iface([], {
  "name": "string",
});

export const TestPlanResponse = t.iface([], {
});

export const SetCameraTransformTestCommand = t.iface([], {
  "position": t.union("SetCameraTransformTestCommand_Vector3", "undefined"),
  "rotation": t.union("SetCameraTransformTestCommand_Quaternion", "undefined"),
});

export const SetCameraTransformTestCommand_Vector3 = t.iface([], {
  "x": "number",
  "y": "number",
  "z": "number",
});

export const SetCameraTransformTestCommand_Quaternion = t.iface([], {
  "x": "number",
  "y": "number",
  "z": "number",
  "w": "number",
});

export const SetCameraTransformTestCommandResponse = t.iface([], {
});

export const RequestTeleportRequest = t.iface([], {
  "destination": "string",
});

export const RequestTeleportResponse = t.iface([], {
});

export const Snapshots = t.iface([], {
  "face256": "string",
  "body": "string",
});

export const AvatarForUserData = t.iface([], {
  "bodyShape": "string",
  "skinColor": "string",
  "hairColor": "string",
  "eyeColor": "string",
  "wearables": t.array("string"),
  "snapshots": t.union("Snapshots", "undefined"),
});

export const UserData = t.iface([], {
  "displayName": "string",
  "publicKey": t.opt(t.union("string", "undefined")),
  "hasConnectedWeb3": "boolean",
  "userId": "string",
  "version": "number",
  "avatar": t.union("AvatarForUserData", "undefined"),
});

export const GetUserDataRequest = t.iface([], {
});

export const GetUserDataResponse = t.iface([], {
  "data": t.opt(t.union("UserData", "undefined")),
});

export const GetUserPublicKeyRequest = t.iface([], {
});

export const GetUserPublicKeyResponse = t.iface([], {
  "address": t.opt(t.union("string", "undefined")),
});

const exportedTypeSuite: ITypeSuite = {
  VideoTrackSourceType,
  VideoTracksActiveStreamsRequest,
  VideoTracksActiveStreamsResponse,
  VideoTracksActiveStreamsData,
  RealSendRequest,
  RealSendResponse,
  SendBinaryRequest,
  SendBinaryResponse,
  Position,
  Vector3,
  Vector2,
  Quaternion,
  Color3,
  Color4,
  ECS6Color4,
  Area,
  UiValue,
  UiValue_UiValueType,
  ECS6ComponentAvatarModifierArea,
  ECS6ComponentTransform,
  ECS6ComponentAttachToAvatar,
  ECS6ComponentAttachToAvatar_AttachToAvatarAnchorPointId,
  ECS6ComponentBillboard,
  ECS6ComponentBoxShape,
  ECS6ComponentSphereShape,
  ECS6ComponentCircleShape,
  ECS6ComponentPlaneShape,
  ECS6ComponentConeShape,
  ECS6ComponentCylinderShape,
  ECS6ComponentGltfShape,
  ECS6ComponentNftShape,
  ECS6ComponentNftShape_PictureFrameStyle,
  ECS6ComponentTexture,
  ECS6ComponentAnimator,
  ECS6ComponentAnimator_AnimationState,
  ECS6ComponentObjShape,
  ECS6ComponentFont,
  ECS6ComponentTextShape,
  ECS6ComponentMaterial,
  ECS6ComponentBasicMaterial,
  ECS6ComponentUuidCallback,
  ECS6ComponentSmartItem,
  ECS6ComponentVideoClip,
  ECS6ComponentVideoTexture,
  ECS6ComponentVideoTexture_VideoStatus,
  ECS6ComponentCameraModeArea,
  ECS6ComponentCameraModeArea_CameraMode,
  ECS6ComponentAvatarTexture,
  ECS6ComponentAudioClip,
  ECS6ComponentAudioSource,
  ECS6ComponentAudioStream,
  ECS6ComponentAvatarShape,
  ECS6ComponentAvatarShape_Emote,
  ECS6ComponentGizmos,
  ECS6ComponentUiShape,
  ECS6ComponentUiContainerRect,
  ECS6ComponentUiContainerStack,
  ECS6ComponentUiContainerStack_UIStackOrientation,
  ECS6ComponentUiButton,
  ECS6ComponentUiText,
  ECS6ComponentUiInputText,
  ECS6ComponentUiImage,
  ECS6ComponentUiScrollRect,
  ECS6ComponentUiWorldSpaceShape,
  ECS6ComponentUiScreenSpaceShape,
  ECS6ComponentUiFullScreenShape,
  OpenExternalUrlBody,
  OpenNFTDialogBody,
  ComponentBodyPayload,
  CreateEntityBody,
  RemoveEntityBody,
  UpdateEntityComponentBody,
  AttachEntityComponentBody,
  ComponentRemovedBody,
  SetEntityParentBody,
  QueryBody,
  QueryBody_Ray,
  QueryBody_RayQuery,
  ComponentCreatedBody,
  ComponentDisposedBody,
  ComponentUpdatedBody,
  InitMessagesFinishedBody,
  EntityActionPayload,
  EntityAction,
  EventDataType,
  ManyEntityAction,
  SendBatchResponse,
  UnsubscribeRequest,
  SubscribeRequest,
  SubscribeResponse,
  UnsubscribeResponse,
  GenericPayload,
  ReadOnlyVector3,
  ReadOnlyQuaternion,
  PositionChangedPayload,
  RotationChangedPayload,
  EventData,
  CrdtSendToRendererRequest,
  CrdtSendToResponse,
  CrdtGetStateRequest,
  CrdtGetStateResponse,
  CrdtMessageFromRendererRequest,
  CrdtMessageFromRendererResponse,
  IsServerRequest,
  IsServerResponse,
  ContentMapping,
  MinimalRunnableEntity,
  BootstrapDataResponse,
  PreviewModeResponse,
  AreUnsafeRequestAllowedResponse,
  GetPlatformResponse,
  EnvironmentRealm,
  GetCurrentRealmResponse,
  GetExplorerConfigurationResponse,
  GetExplorerConfigurationResponse_ConfigurationsEntry,
  GetDecentralandTimeResponse,
  GetBootstrapDataRequest,
  IsPreviewModeRequest,
  GetPlatformRequest,
  AreUnsafeRequestAllowedRequest,
  GetCurrentRealmRequest,
  GetExplorerConfigurationRequest,
  GetDecentralandTimeRequest,
  RequirePaymentRequest,
  RequirePaymentResponse,
  SignMessageRequest,
  SignMessageRequest_MessageEntry,
  SignMessageResponse,
  ConvertMessageToObjectRequest,
  ConvertMessageToObjectResponse,
  ConvertMessageToObjectResponse_DictEntry,
  SendAsyncRequest,
  SendAsyncResponse,
  GetUserAccountRequest,
  GetUserAccountResponse,
  Player,
  PlayersGetUserDataResponse,
  PlayerListResponse,
  GetPlayerDataRequest,
  GetPlayersInSceneRequest,
  GetConnectedPlayersRequest,
  KillRequest,
  KillResponse,
  SpawnRequest,
  SpawnResponse,
  PxRequest,
  GetPortableExperiencesLoadedRequest,
  GetPortableExperiencesLoadedResponse,
  ExitRequest,
  ExitResponse,
  MovePlayerToRequest,
  TeleportToRequest,
  TriggerEmoteRequest,
  ChangeRealmRequest,
  OpenExternalUrlRequest,
  OpenNftDialogRequest,
  UnblockPointerRequest,
  CommsAdapterRequest,
  TriggerSceneEmoteRequest,
  SuccessResponse,
  TriggerEmoteResponse,
  MovePlayerToResponse,
  TeleportToResponse,
  RealmInfo,
  GetRealmResponse,
  GetWorldTimeResponse,
  GetRealmRequest,
  GetWorldTimeRequest,
  ReadFileRequest,
  ReadFileResponse,
  CurrentSceneEntityRequest,
  CurrentSceneEntityResponse,
  GetExplorerInformationRequest,
  GetExplorerInformationResponse,
  GetExplorerInformationResponse_ConfigurationsEntry,
  GetSceneRequest,
  GetSceneResponse,
  FlatFetchInit,
  FlatFetchInit_HeadersEntry,
  FlatFetchResponse,
  FlatFetchResponse_HeadersEntry,
  SignedFetchRequest,
  GetHeadersResponse,
  GetHeadersResponse_HeadersEntry,
  TestResult,
  TestResultResponse,
  TestPlan,
  TestPlan_TestPlanEntry,
  TestPlanResponse,
  SetCameraTransformTestCommand,
  SetCameraTransformTestCommand_Vector3,
  SetCameraTransformTestCommand_Quaternion,
  SetCameraTransformTestCommandResponse,
  RequestTeleportRequest,
  RequestTeleportResponse,
  Snapshots,
  AvatarForUserData,
  UserData,
  GetUserDataRequest,
  GetUserDataResponse,
  GetUserPublicKeyRequest,
  GetUserPublicKeyResponse,
};
export default exportedTypeSuite;

export function typeSuites(): ITypeSuite {
  return exportedTypeSuite
}