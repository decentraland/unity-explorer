
/**
  * CommsApi
  */


export enum VideoTrackSourceType {
    VTST_UNKNOWN = 0,
    VTST_CAMERA = 1,
    VTST_SCREEN_SHARE = 2,
    UNRECOGNIZED = -1
}
export interface VideoTracksActiveStreamsRequest {
}
export interface VideoTracksActiveStreamsResponse {
    streams: VideoTracksActiveStreamsData[];
}
export interface VideoTracksActiveStreamsData {
    identity: string;
    trackSid: string;
    sourceType: VideoTrackSourceType;
}

// Function declaration section
export function getActiveVideoStreams(body: VideoTracksActiveStreamsRequest): Promise<VideoTracksActiveStreamsResponse>;

/**
  * CommunicationsController
  */


export interface RealSendRequest {
    message: string;
}
export interface RealSendResponse {
}
export interface SendBinaryRequest {
    data: Uint8Array[];
}
export interface SendBinaryResponse {
    data: Uint8Array[];
}

// Function declaration section
/**
* @deprecated - This API should use a bidirectional binary stream in sdk7
* https://github.com/decentraland/sdk/issues/582
*/
export function send(body: RealSendRequest): Promise<RealSendResponse>;
export function sendBinary(body: SendBinaryRequest): Promise<SendBinaryResponse>;


/**
  * EngineApi
  */


export interface Position {
    x: number;
    y: number;
    z: number;
}
export interface Vector3 {
    x: number;
    y: number;
    z: number;
}
export interface Vector2 {
    x: number;
    y: number;
}
export interface Quaternion {
    x: number;
    y: number;
    z: number;
    w: number;
}

// Function declaration section
export interface Color3 {
    r: number;
    g: number;
    b: number;
}
export interface Color4 {
    r: number;
    g: number;
    b: number;
    a: number;
}

// Function declaration section
export interface ECS6Color4 {
    r: number;
    g: number;
    b: number;
    a?: number | undefined;
}
export interface Area {
    box?: Vector3 | undefined;
}
export interface UiValue {
    value?: number | undefined;
    type?: UiValue_UiValueType | undefined;
}
export enum UiValue_UiValueType {
    UVT_PERCENT = 0,
    PIXELS = 1,
    UNRECOGNIZED = -1
}
/** CLASS_ID.AVATAR_MODIFIER_AREA */
export interface ECS6ComponentAvatarModifierArea {
    area?: Area | undefined;
    modifiers: string[];
    excludeIds: string[];
}
/** CLASS_ID.TRANSFORM */
export interface ECS6ComponentTransform {
    position?: Vector3 | undefined;
    rotation?: Quaternion | undefined;
    scale?: Vector3 | undefined;
}
/** CLASS_ID.AVATAR_ATTACH */
export interface ECS6ComponentAttachToAvatar {
    avatarId?: string | undefined;
    anchorPointId?: ECS6ComponentAttachToAvatar_AttachToAvatarAnchorPointId | undefined;
    avatarSceneId?: string | undefined;
}
export enum ECS6ComponentAttachToAvatar_AttachToAvatarAnchorPointId {
    ATAAP_POSITION = 0,
    ATAAP_NAME_TAG = 1,
    ATAAP_LEFT_HAND = 2,
    ATAAP_RIGHT_HAND = 3,
    UNRECOGNIZED = -1
}
/** CLASS_ID.BILLBOARD */
export interface ECS6ComponentBillboard {
    x?: boolean | undefined;
    y?: boolean | undefined;
    z?: boolean | undefined;
}
/** CLASS_ID.BOX_SHAPE */
export interface ECS6ComponentBoxShape {
    withCollisions?: boolean | undefined;
    isPointerBlocker?: boolean | undefined;
    visible?: boolean | undefined;
    uvs: number[];
}
/** CLASS_ID.SPHERE_SHAPE */
export interface ECS6ComponentSphereShape {
    withCollisions?: boolean | undefined;
    isPointerBlocker?: boolean | undefined;
    visible?: boolean | undefined;
}
/** CLASS_ID.CIRCLE_SHAPE */
export interface ECS6ComponentCircleShape {
    withCollisions?: boolean | undefined;
    isPointerBlocker?: boolean | undefined;
    visible?: boolean | undefined;
    segments?: number | undefined;
    arc?: number | undefined;
}
/** CLASS_ID.PLANE_SHAPE */
export interface ECS6ComponentPlaneShape {
    withCollisions?: boolean | undefined;
    isPointerBlocker?: boolean | undefined;
    visible?: boolean | undefined;
    uvs: number[];
    width?: number | undefined;
    height?: number | undefined;
}
/** CLASS_ID.CONE_SHAPE */
export interface ECS6ComponentConeShape {
    withCollisions?: boolean | undefined;
    isPointerBlocker?: boolean | undefined;
    visible?: boolean | undefined;
    radiusTop?: number | undefined;
    radiusBottom?: number | undefined;
    segmentsHeight?: number | undefined;
    segmentsRadial?: number | undefined;
    openEnded?: boolean | undefined;
    radius?: number | undefined;
    arc?: number | undefined;
}
/** CLASS_ID.CYLINDER_SHAPE */
export interface ECS6ComponentCylinderShape {
    withCollisions?: boolean | undefined;
    isPointerBlocker?: boolean | undefined;
    visible?: boolean | undefined;
    radiusTop?: number | undefined;
    radiusBottom?: number | undefined;
    segmentsHeight?: number | undefined;
    segmentsRadial?: number | undefined;
    openEnded?: boolean | undefined;
    radius?: number | undefined;
    arc?: number | undefined;
}
/** CLASS_ID.GLTF_SHAPE */
export interface ECS6ComponentGltfShape {
    withCollisions?: boolean | undefined;
    isPointerBlocker?: boolean | undefined;
    visible?: boolean | undefined;
    src?: string | undefined;
}
/** CLASS_ID.NFT_SHAPE */
export interface ECS6ComponentNftShape {
    withCollisions?: boolean | undefined;
    isPointerBlocker?: boolean | undefined;
    visible?: boolean | undefined;
    src?: string | undefined;
    style?: ECS6ComponentNftShape_PictureFrameStyle | undefined;
    color?: Color3 | undefined;
}
export enum ECS6ComponentNftShape_PictureFrameStyle {
    PFS_CLASSIC = 0,
    PFS_BAROQUE_ORNAMENT = 1,
    PFS_DIAMOND_ORNAMENT = 2,
    PFS_MINIMAL_WIDE = 3,
    PFS_MINIMAL_GREY = 4,
    PFS_BLOCKY = 5,
    PFS_GOLD_EDGES = 6,
    PFS_GOLD_CARVED = 7,
    PFS_GOLD_WIDE = 8,
    PFS_GOLD_ROUNDED = 9,
    PFS_METAL_MEDIUM = 10,
    PFS_METAL_WIDE = 11,
    PFS_METAL_SLIM = 12,
    PFS_METAL_ROUNDED = 13,
    PFS_PINS = 14,
    PFS_MINIMAL_BLACK = 15,
    PFS_MINIMAL_WHITE = 16,
    PFS_TAPE = 17,
    PFS_WOOD_SLIM = 18,
    PFS_WOOD_WIDE = 19,
    PFS_WOOD_TWIGS = 20,
    PFS_CANVAS = 21,
    PFS_NONE = 22,
    UNRECOGNIZED = -1
}
/** CLASS_ID.TEXTURE */
export interface ECS6ComponentTexture {
    src?: string | undefined;
    samplingMode?: number | undefined;
    wrap?: number | undefined;
    hasAlpha?: boolean | undefined;
}
/** CLASS_ID.ANIMATION */
export interface ECS6ComponentAnimator {
    states: ECS6ComponentAnimator_AnimationState[];
}
export interface ECS6ComponentAnimator_AnimationState {
    clip?: string | undefined;
    looping?: boolean | undefined;
    weight?: number | undefined;
    playing?: boolean | undefined;
    shouldReset?: boolean | undefined;
    speed?: number | undefined;
    name?: string | undefined;
}
/** CLASS_ID.OBJ_SHAPE */
export interface ECS6ComponentObjShape {
    withCollisions?: boolean | undefined;
    isPointerBlocker?: boolean | undefined;
    visible?: boolean | undefined;
    src?: string | undefined;
}
/** CLASS_ID.FONT */
export interface ECS6ComponentFont {
    src?: string | undefined;
}
/** CLASS_ID.TEXT_SHAPE */
export interface ECS6ComponentTextShape {
    outlineWidth?: number | undefined;
    outlineColor?: Color3 | undefined;
    color?: Color3 | undefined;
    fontSize?: number | undefined;
    font?: string | undefined;
    opacity?: number | undefined;
    value?: string | undefined;
    lineSpacing?: string | undefined;
    lineCount?: number | undefined;
    textWrapping?: boolean | undefined;
    shadowBlur?: number | undefined;
    shadowOffsetX?: number | undefined;
    shadowOffsetY?: number | undefined;
    shadowColor?: Color3 | undefined;
    hTextAlign?: string | undefined;
    vTextAlign?: string | undefined;
    width?: number | undefined;
    height?: number | undefined;
    paddingTop?: number | undefined;
    paddingRight?: number | undefined;
    paddingBottom?: number | undefined;
    paddingLeft?: number | undefined;
    billboard?: boolean | undefined;
    visible?: boolean | undefined;
}
/** CLASS_ID.PBR_MATERIAL */
export interface ECS6ComponentMaterial {
    alphaTest?: number | undefined;
    albedoColor?: ECS6Color4 | undefined;
    emissiveColor?: Color3 | undefined;
    metallic?: number | undefined;
    roughness?: number | undefined;
    reflectivityColor?: Color3 | undefined;
    directIntensity?: number | undefined;
    microSurface?: number | undefined;
    emissiveIntensity?: number | undefined;
    specularIntensity?: number | undefined;
    albedoTexture?: string | undefined;
    alphaTexture?: string | undefined;
    emissiveTexture?: string | undefined;
    bumpTexture?: string | undefined;
    transparencyMode?: number | undefined;
    castShadows?: boolean | undefined;
}
/** CLASS_ID.BASIC_MATERIAL */
export interface ECS6ComponentBasicMaterial {
    alphaTest?: number | undefined;
    texture?: string | undefined;
    castShadows?: boolean | undefined;
}
/** CLASS_ID.UUID_CALLBACK */
export interface ECS6ComponentUuidCallback {
    button?: string | undefined;
    hoverText?: string | undefined;
    distance?: number | undefined;
    showFeedback?: boolean | undefined;
    type?: string | undefined;
    uuid?: string | undefined;
}
/** CLASS_ID.SMART_ITEM) */
export interface ECS6ComponentSmartItem {
}
/** CLASS_ID.VIDEO_CLIP */
export interface ECS6ComponentVideoClip {
    url?: string | undefined;
}
/** CLASS_ID.VIDEO_TEXTURE */
export interface ECS6ComponentVideoTexture {
    samplingMode?: number | undefined;
    wrap?: number | undefined;
    volume?: number | undefined;
    playbackRate?: number | undefined;
    seek?: number | undefined;
    playing?: boolean | undefined;
    loop?: boolean | undefined;
    videoClipId?: string | undefined;
}
export enum ECS6ComponentVideoTexture_VideoStatus {
    NONE = 0,
    ERROR = 1,
    LOADING = 2,
    READY = 3,
    PLAYING = 4,
    BUFFERING = 5,
    UNRECOGNIZED = -1
}
/** CLASS_ID.CAMERA_MODE_AREA */
export interface ECS6ComponentCameraModeArea {
    area: Area | undefined;
    cameraMode: ECS6ComponentCameraModeArea_CameraMode;
}
export enum ECS6ComponentCameraModeArea_CameraMode {
    CM_FIRST_PERSON = 0,
    CM_THIRD_PERSON = 1,
    CM_BUILDING_TOOL_GOD_MODE = 2,
    UNRECOGNIZED = -1
}
/** CLASS_ID.AVATAR_TEXTURE */
export interface ECS6ComponentAvatarTexture {
    samplingMode?: number | undefined;
    wrap?: number | undefined;
    hasAlpha?: boolean | undefined;
    userId?: string | undefined;
}
/** CLASS_ID.AUDIO_CLIP */
export interface ECS6ComponentAudioClip {
    url?: string | undefined;
    loop?: boolean | undefined;
    loadingCompleteEventId?: string | undefined;
    volume?: number | undefined;
}
/** CLASS_ID.AUDIO_SOURCE */
export interface ECS6ComponentAudioSource {
    audioClipId?: string | undefined;
    loop?: boolean | undefined;
    volume?: number | undefined;
    playing?: boolean | undefined;
    pitch?: number | undefined;
    playedAtTimestamp?: number | undefined;
}
/** CLASS_ID.AUDIO_STREAM */
export interface ECS6ComponentAudioStream {
    url?: string | undefined;
    playing?: boolean | undefined;
    volume?: number | undefined;
}
/** CLASS_ID.AVATAR_SHAPE */
export interface ECS6ComponentAvatarShape {
    id?: string | undefined;
    name?: string | undefined;
    expressionTriggerId?: string | undefined;
    expressionTriggerTimestamp?: number | undefined;
    bodyShape?: string | undefined;
    wearables: string[];
    emotes: ECS6ComponentAvatarShape_Emote[];
    skinColor?: ECS6Color4 | undefined;
    hairColor?: ECS6Color4 | undefined;
    eyeColor?: ECS6Color4 | undefined;
    useDummyModel?: boolean | undefined;
    talking?: boolean | undefined;
}
export interface ECS6ComponentAvatarShape_Emote {
    slot?: number | undefined;
    urn?: string | undefined;
}
/** CLASS_ID.GIZMOS */
export interface ECS6ComponentGizmos {
    position?: boolean | undefined;
    rotation?: boolean | undefined;
    scale?: boolean | undefined;
    cycle?: boolean | undefined;
    selectedGizmo?: string | undefined;
    localReference?: boolean | undefined;
}
/** NO CLASS */
export interface ECS6ComponentUiShape {
    name?: string | undefined;
    visible?: boolean | undefined;
    opacity?: number | undefined;
    hAlign?: string | undefined;
    vAlign?: string | undefined;
    width?: UiValue | undefined;
    height?: UiValue | undefined;
    positionX?: UiValue | undefined;
    positionY?: UiValue | undefined;
    isPointerBlocker?: boolean | undefined;
    parentComponent?: string | undefined;
}
/** CLASS_ID.UI_CONTAINER_RECT */
export interface ECS6ComponentUiContainerRect {
    /** UiShape */
    name?: string | undefined;
    visible?: boolean | undefined;
    opacity?: number | undefined;
    hAlign?: string | undefined;
    vAlign?: string | undefined;
    width?: UiValue | undefined;
    height?: UiValue | undefined;
    positionX?: UiValue | undefined;
    positionY?: UiValue | undefined;
    isPointerBlocker?: boolean | undefined;
    parentComponent?: string | undefined;
    /** UiShape */
    thickness?: number | undefined;
    color?: ECS6Color4 | undefined;
    alignmentUsesSize?: boolean | undefined;
}
/** CLASS_ID.UI_CONTAINER_STACK */
export interface ECS6ComponentUiContainerStack {
    /** UiShape */
    name?: string | undefined;
    visible?: boolean | undefined;
    opacity?: number | undefined;
    hAlign?: string | undefined;
    vAlign?: string | undefined;
    width?: UiValue | undefined;
    height?: UiValue | undefined;
    positionX?: UiValue | undefined;
    positionY?: UiValue | undefined;
    isPointerBlocker?: boolean | undefined;
    /** UiShape */
    parentComponent?: string | undefined;
    adaptWidth?: boolean | undefined;
    adaptHeight?: boolean | undefined;
    color?: ECS6Color4 | undefined;
    stackOrientation?: ECS6ComponentUiContainerStack_UIStackOrientation | undefined;
    spacing?: number | undefined;
}
export enum ECS6ComponentUiContainerStack_UIStackOrientation {
    VERTICAL = 0,
    HORIZONTAL = 1,
    UNRECOGNIZED = -1
}
/** CLASS_ID.UI_BUTTON_SHAPE */
export interface ECS6ComponentUiButton {
    /** UiShape */
    name?: string | undefined;
    visible?: boolean | undefined;
    opacity?: number | undefined;
    hAlign?: string | undefined;
    vAlign?: string | undefined;
    width?: UiValue | undefined;
    height?: UiValue | undefined;
    positionX?: UiValue | undefined;
    positionY?: UiValue | undefined;
    isPointerBlocker?: boolean | undefined;
    /** UiShape */
    parentComponent?: string | undefined;
    fontSize?: number | undefined;
    fontWeight?: string | undefined;
    thickness?: number | undefined;
    cornerRadius?: number | undefined;
    color?: ECS6Color4 | undefined;
    background?: ECS6Color4 | undefined;
    paddingTop?: number | undefined;
    paddingRight?: number | undefined;
    paddingBottom?: number | undefined;
    paddingLeft?: number | undefined;
    shadowBlur?: number | undefined;
    shadowOffsetX?: number | undefined;
    shadowOffsetY?: number | undefined;
    shadowColor?: ECS6Color4 | undefined;
    text?: string | undefined;
}
/** CLASS_ID.UI_TEXT_SHAPE */
export interface ECS6ComponentUiText {
    /** UiShape */
    name?: string | undefined;
    visible?: boolean | undefined;
    opacity?: number | undefined;
    hAlign?: string | undefined;
    vAlign?: string | undefined;
    width?: UiValue | undefined;
    height?: UiValue | undefined;
    positionX?: UiValue | undefined;
    positionY?: UiValue | undefined;
    isPointerBlocker?: boolean | undefined;
    /** UiShape */
    parentComponent?: string | undefined;
    outlineWidth?: number | undefined;
    outlineColor?: ECS6Color4 | undefined;
    color?: ECS6Color4 | undefined;
    fontSize?: number | undefined;
    fontAutoSize?: boolean | undefined;
    font?: string | undefined;
    value?: string | undefined;
    lineSpacing?: number | undefined;
    lineCount?: number | undefined;
    adaptWidth?: boolean | undefined;
    adaptHeight?: boolean | undefined;
    textWrapping?: boolean | undefined;
    shadowBlur?: number | undefined;
    shadowOffsetX?: number | undefined;
    shadowOffsetY?: number | undefined;
    shadowColor?: ECS6Color4 | undefined;
    hTextAlign?: string | undefined;
    vTextAlign?: string | undefined;
    paddingTop?: number | undefined;
    paddingRight?: number | undefined;
    paddingBottom?: number | undefined;
    paddingLeft?: number | undefined;
}
/** CLASS_ID.UI_INPUT_TEXT_SHAPE */
export interface ECS6ComponentUiInputText {
    /** UiShape */
    name?: string | undefined;
    visible?: boolean | undefined;
    opacity?: number | undefined;
    hAlign?: string | undefined;
    vAlign?: string | undefined;
    width?: UiValue | undefined;
    height?: UiValue | undefined;
    positionX?: UiValue | undefined;
    positionY?: UiValue | undefined;
    isPointerBlocker?: boolean | undefined;
    /** UiShape */
    parentComponent?: string | undefined;
    outlineWidth?: number | undefined;
    outlineColor?: ECS6Color4 | undefined;
    color?: ECS6Color4 | undefined;
    fontSize?: number | undefined;
    font?: string | undefined;
    value?: string | undefined;
    placeholder?: string | undefined;
    margin?: number | undefined;
    focusedBackground?: ECS6Color4 | undefined;
    textWrapping?: boolean | undefined;
    shadowBlur?: number | undefined;
    shadowOffsetX?: number | undefined;
    shadowOffsetY?: number | undefined;
    shadowColor?: ECS6Color4 | undefined;
    hTextAlign?: string | undefined;
    vTextAlign?: string | undefined;
    paddingTop?: number | undefined;
    paddingRight?: number | undefined;
    paddingBottom?: number | undefined;
    paddingLeft?: number | undefined;
    onTextChanged?: string | undefined;
    onFocus?: string | undefined;
    onBlur?: string | undefined;
    onTextSubmit?: string | undefined;
    onChanged?: string | undefined;
}
/** CLASS_ID.UI_IMAGE_SHAPE */
export interface ECS6ComponentUiImage {
    /** UiShape */
    name?: string | undefined;
    visible?: boolean | undefined;
    opacity?: number | undefined;
    hAlign?: string | undefined;
    vAlign?: string | undefined;
    width?: UiValue | undefined;
    height?: UiValue | undefined;
    positionX?: UiValue | undefined;
    positionY?: UiValue | undefined;
    isPointerBlocker?: boolean | undefined;
    /** UiShape */
    parentComponent?: string | undefined;
    sourceLeft?: number | undefined;
    sourceTop?: number | undefined;
    sourceWidth?: number | undefined;
    sourceHeight?: number | undefined;
    source?: string | undefined;
    paddingTop?: number | undefined;
    paddingRight?: number | undefined;
    paddingBottom?: number | undefined;
    paddingLeft?: number | undefined;
    sizeInPixels?: boolean | undefined;
    onClick?: string | undefined;
}
/** CLASS_ID.UI_SLIDER_SHAPE */
export interface ECS6ComponentUiScrollRect {
    /** UiShape */
    name?: string | undefined;
    visible?: boolean | undefined;
    opacity?: number | undefined;
    hAlign?: string | undefined;
    vAlign?: string | undefined;
    width?: UiValue | undefined;
    height?: UiValue | undefined;
    positionX?: UiValue | undefined;
    positionY?: UiValue | undefined;
    isPointerBlocker?: boolean | undefined;
    /** UiShape */
    parentComponent?: string | undefined;
    valueX?: number | undefined;
    valueY?: number | undefined;
    backgroundColor?: ECS6Color4 | undefined;
    isHorizontal?: boolean | undefined;
    isVertical?: boolean | undefined;
    paddingTop?: number | undefined;
    paddingRight?: number | undefined;
    paddingBottom?: number | undefined;
    paddingLeft?: number | undefined;
    onChanged?: string | undefined;
}
/** CLASS_ID.UI_WORLD_SPACE_SHAPE */
export interface ECS6ComponentUiWorldSpaceShape {
    /** UiShape */
    name?: string | undefined;
    visible?: boolean | undefined;
    opacity?: number | undefined;
    hAlign?: string | undefined;
    vAlign?: string | undefined;
    width?: UiValue | undefined;
    height?: UiValue | undefined;
    positionX?: UiValue | undefined;
    positionY?: UiValue | undefined;
    isPointerBlocker?: boolean | undefined;
    /** UiShape */
    parentComponent?: string | undefined;
}
/** CLASS_ID.UI_SCREEN_SPACE_SHAPE */
export interface ECS6ComponentUiScreenSpaceShape {
    /** UiShape */
    name?: string | undefined;
    visible?: boolean | undefined;
    opacity?: number | undefined;
    hAlign?: string | undefined;
    vAlign?: string | undefined;
    width?: UiValue | undefined;
    height?: UiValue | undefined;
    positionX?: UiValue | undefined;
    positionY?: UiValue | undefined;
    isPointerBlocker?: boolean | undefined;
    /** UiShape */
    parentComponent?: string | undefined;
}
/** CLASS_ID.UI_FULLSCREEN_SHAPE */
export interface ECS6ComponentUiFullScreenShape {
    /** UiShape */
    name?: string | undefined;
    visible?: boolean | undefined;
    opacity?: number | undefined;
    hAlign?: string | undefined;
    vAlign?: string | undefined;
    width?: UiValue | undefined;
    height?: UiValue | undefined;
    positionX?: UiValue | undefined;
    positionY?: UiValue | undefined;
    isPointerBlocker?: boolean | undefined;
    /** UiShape */
    parentComponent?: string | undefined;
}

// Function declaration section
export interface OpenExternalUrlBody {
    url: string;
}
export interface OpenNFTDialogBody {
    assetContractAddress: string;
    tokenId: string;
    comment?: string | undefined;
}
export interface ComponentBodyPayload {
    avatarModifierArea?: ECS6ComponentAvatarModifierArea | undefined;
    transform?: ECS6ComponentTransform | undefined;
    attachToAvatar?: ECS6ComponentAttachToAvatar | undefined;
    billboard?: ECS6ComponentBillboard | undefined;
    boxShape?: ECS6ComponentBoxShape | undefined;
    sphereShape?: ECS6ComponentSphereShape | undefined;
    circleShape?: ECS6ComponentCircleShape | undefined;
    planeShape?: ECS6ComponentPlaneShape | undefined;
    coneShape?: ECS6ComponentConeShape | undefined;
    cylinderShape?: ECS6ComponentCylinderShape | undefined;
    gltfShape?: ECS6ComponentGltfShape | undefined;
    nftShape?: ECS6ComponentNftShape | undefined;
    texture?: ECS6ComponentTexture | undefined;
    animator?: ECS6ComponentAnimator | undefined;
    objShape?: ECS6ComponentObjShape | undefined;
    font?: ECS6ComponentFont | undefined;
    textShape?: ECS6ComponentTextShape | undefined;
    material?: ECS6ComponentMaterial | undefined;
    basicMaterial?: ECS6ComponentBasicMaterial | undefined;
    uuidCallback?: ECS6ComponentUuidCallback | undefined;
    smartItem?: ECS6ComponentSmartItem | undefined;
    videoClip?: ECS6ComponentVideoClip | undefined;
    videoTexture?: ECS6ComponentVideoTexture | undefined;
    cameraModeArea?: ECS6ComponentCameraModeArea | undefined;
    avatarTexture?: ECS6ComponentAvatarTexture | undefined;
    audioClip?: ECS6ComponentAudioClip | undefined;
    audioSource?: ECS6ComponentAudioSource | undefined;
    audioStream?: ECS6ComponentAudioStream | undefined;
    avatarShape?: ECS6ComponentAvatarShape | undefined;
    gizmos?: ECS6ComponentGizmos | undefined;
    uiShape?: ECS6ComponentUiShape | undefined;
    uiContainerRect?: ECS6ComponentUiContainerRect | undefined;
    uiContainerStack?: ECS6ComponentUiContainerStack | undefined;
    uiButton?: ECS6ComponentUiButton | undefined;
    uiText?: ECS6ComponentUiText | undefined;
    uiInputText?: ECS6ComponentUiInputText | undefined;
    uiImage?: ECS6ComponentUiImage | undefined;
    uiScrollRect?: ECS6ComponentUiScrollRect | undefined;
    uiWorldSpaceShape?: ECS6ComponentUiWorldSpaceShape | undefined;
    uiScreenSpaceShape?: ECS6ComponentUiScreenSpaceShape | undefined;
    uiFullScreenShape?: ECS6ComponentUiFullScreenShape | undefined;
}
export interface CreateEntityBody {
    id: string;
}
export interface RemoveEntityBody {
    id: string;
}
export interface UpdateEntityComponentBody {
    entityId: string;
    classId: number;
    name: string;
    componentData: ComponentBodyPayload | undefined;
}
export interface AttachEntityComponentBody {
    entityId: string;
    name: string;
    id: string;
}
export interface ComponentRemovedBody {
    entityId: string;
    name: string;
}
export interface SetEntityParentBody {
    entityId: string;
    parentId: string;
}
export interface QueryBody {
    queryId: string;
    payload: QueryBody_RayQuery | undefined;
}
export interface QueryBody_Ray {
    origin: Vector3 | undefined;
    direction: Vector3 | undefined;
    distance: number;
}
export interface QueryBody_RayQuery {
    queryId: string;
    queryType: string;
    ray: QueryBody_Ray | undefined;
}
export interface ComponentCreatedBody {
    id: string;
    classId: number;
    name: string;
}
export interface ComponentDisposedBody {
    id: string;
}
export interface ComponentUpdatedBody {
    id: string;
    componentData: ComponentBodyPayload | undefined;
}
export interface InitMessagesFinishedBody {
}
export interface EntityActionPayload {
    openExternalUrl?: OpenExternalUrlBody | undefined;
    openNftDialog?: OpenNFTDialogBody | undefined;
    createEntity?: CreateEntityBody | undefined;
    removeEntity?: RemoveEntityBody | undefined;
    updateEntityComponent?: UpdateEntityComponentBody | undefined;
    attachEntityComponent?: AttachEntityComponentBody | undefined;
    componentRemoved?: ComponentRemovedBody | undefined;
    setEntityParent?: SetEntityParentBody | undefined;
    query?: QueryBody | undefined;
    componentCreated?: ComponentCreatedBody | undefined;
    componentDisposed?: ComponentDisposedBody | undefined;
    componentUpdated?: ComponentUpdatedBody | undefined;
    initMessagesFinished?: InitMessagesFinishedBody | undefined;
}
export interface EntityAction {
    tag?: string | undefined;
    payload: EntityActionPayload | undefined;
}

// Function declaration section
/** Events */
export enum EventDataType {
    EDT_GENERIC = 0,
    EDT_POSITION_CHANGED = 1,
    EDT_ROTATION_CHANGED = 2,
    UNRECOGNIZED = -1
}
export interface ManyEntityAction {
    actions: EntityAction[];
}
export interface SendBatchResponse {
    events: EventData[];
}
export interface UnsubscribeRequest {
    eventId: string;
}
export interface SubscribeRequest {
    eventId: string;
}
export interface SubscribeResponse {
}
export interface UnsubscribeResponse {
}
export interface GenericPayload {
    eventId: string;
    eventData: string;
}
export interface ReadOnlyVector3 {
    x: number;
    y: number;
    z: number;
}
export interface ReadOnlyQuaternion {
    x: number;
    y: number;
    z: number;
    w: number;
}
export interface PositionChangedPayload {
    position: ReadOnlyVector3 | undefined;
    cameraPosition: ReadOnlyVector3 | undefined;
    playerHeight: number;
}
export interface RotationChangedPayload {
    rotation: ReadOnlyVector3 | undefined;
    quaternion: ReadOnlyQuaternion | undefined;
}
export interface EventData {
    type: EventDataType;
    generic?: GenericPayload | undefined;
    positionChanged?: PositionChangedPayload | undefined;
    rotationChanged?: RotationChangedPayload | undefined;
}
export interface CrdtSendToRendererRequest {
    data: Uint8Array;
}
export interface CrdtSendToResponse {
    /** list of CRDT messages coming back from the renderer */
    data: Uint8Array[];
}
export interface CrdtGetStateRequest {
}
export interface CrdtGetStateResponse {
    /** returns true if the returned state has scene-created entities */
    hasEntities: boolean;
    /** static entities data (root entity, camera, etc) and scene-created entities */
    data: Uint8Array[];
}
/** deprecated */
export interface CrdtMessageFromRendererRequest {
}
/** deprecated */
export interface CrdtMessageFromRendererResponse {
    data: Uint8Array[];
}
export interface IsServerRequest {
}
export interface IsServerResponse {
    isServer: boolean;
}

// Function declaration section
/** @deprecated */
export function sendBatch(body: ManyEntityAction): Promise<SendBatchResponse>;
/** @deprecated */
export function subscribe(body: SubscribeRequest): Promise<SubscribeResponse>;
/** @deprecated */
export function unsubscribe(body: UnsubscribeRequest): Promise<UnsubscribeResponse>;
/**
 * send information of the CRDT messages to the renderer. It returns the CRDT changes back from the renderer
 * like raycast responses or the player's position
 */
export function crdtSendToRenderer(body: CrdtSendToRendererRequest): Promise<CrdtSendToResponse>;
/**
 * retrieves the current _full_ state of the entities from the renderer. This function is used to hidrate
 * the state of the scenes when the code of the worker is stopped/resumed
 */
export function crdtGetState(body: CrdtSendToRendererRequest): Promise<CrdtGetStateResponse>;
/** @deprecated, this response was merged into CrdtSendToResponse */
export function crdtGetMessageFromRenderer(body: CrdtMessageFromRendererRequest): Promise<CrdtMessageFromRendererResponse>;
export function isServer(body: IsServerRequest): Promise<IsServerResponse>;

/**
  * EnvironmentApi
  */

export interface ContentMapping {
    file: string;
    hash: string;
}

// Function declaration section
export interface MinimalRunnableEntity {
    content: ContentMapping[];
    metadataJson: string;
}
export interface BootstrapDataResponse {
    id: string;
    baseUrl: string;
    entity: MinimalRunnableEntity | undefined;
    useFPSThrottling: boolean;
}
export interface PreviewModeResponse {
    isPreview: boolean;
}
export interface AreUnsafeRequestAllowedResponse {
    status: boolean;
}
export interface GetPlatformResponse {
    platform: string;
}
export interface EnvironmentRealm {
    domain: string;
    layer: string;
    room: string;
    serverName: string;
    displayName: string;
    protocol: string;
}
export interface GetCurrentRealmResponse {
    currentRealm?: EnvironmentRealm | undefined;
}
export interface GetExplorerConfigurationResponse {
    clientUri: string;
    configurations: {
        [key: string]: string;
    };
}
export interface GetExplorerConfigurationResponse_ConfigurationsEntry {
    key: string;
    value: string;
}
export interface GetDecentralandTimeResponse {
    seconds: number;
}
export interface GetBootstrapDataRequest {
}
export interface IsPreviewModeRequest {
}
export interface GetPlatformRequest {
}
export interface AreUnsafeRequestAllowedRequest {
}
export interface GetCurrentRealmRequest {
}
export interface GetExplorerConfigurationRequest {
}
export interface GetDecentralandTimeRequest {
}

// Function declaration section
/** @deprecated, only available for SDK6 compatibility. Use runtime_api instead */
export function getBootstrapData(body: GetBootstrapDataRequest): Promise<BootstrapDataResponse>;
/** @deprecated, only available for SDK6 compatibility. Needs migration */
export function isPreviewMode(body: IsPreviewModeRequest): Promise<PreviewModeResponse>;
/** @deprecated, only available for SDK6 compatibility */
export function getPlatform(body: GetPlatformRequest): Promise<GetPlatformResponse>;
/** @deprecated, only available for SDK6 compatibility */
export function areUnsafeRequestAllowed(body: AreUnsafeRequestAllowedRequest): Promise<AreUnsafeRequestAllowedResponse>;
/** @deprecated, use GetCurrentRealm from runtime_api instead */
export function getCurrentRealm(body: GetCurrentRealmRequest): Promise<GetCurrentRealmResponse>;
/** @deprecated, only available for SDK6 compatibility */
export function getExplorerConfiguration(body: GetExplorerConfigurationRequest): Promise<GetExplorerConfigurationResponse>;
/** @deprecated, use GetTime from runtime_api instead */
export function getDecentralandTime(body: GetDecentralandTimeRequest): Promise<GetDecentralandTimeResponse>;


/**
  * EthereumController
  */

export interface RequirePaymentRequest {
    toAddress: string;
    amount: number;
    currency: string;
}
export interface RequirePaymentResponse {
    jsonAnyResponse: string;
}
export interface SignMessageRequest {
    message: {
        [key: string]: string;
    };
}
export interface SignMessageRequest_MessageEntry {
    key: string;
    value: string;
}
export interface SignMessageResponse {
    message: string;
    hexEncodedMessage: string;
    signature: string;
}
export interface ConvertMessageToObjectRequest {
    message: string;
}
export interface ConvertMessageToObjectResponse {
    dict: {
        [key: string]: string;
    };
}
export interface ConvertMessageToObjectResponse_DictEntry {
    key: string;
    value: string;
}
export interface SendAsyncRequest {
    id: number;
    method: string;
    jsonParams: string;
}
export interface SendAsyncResponse {
    jsonAnyResponse: string;
}
export interface GetUserAccountRequest {
}
export interface GetUserAccountResponse {
    address?: string | undefined;
}

// Function declaration section
/**
* @deprecated, only available for SDK6 compatibility. This was a low level API that can
* be replaced by any ethereum library on top of the provider
*/
export function requirePayment(body: RequirePaymentRequest): Promise<RequirePaymentResponse>;
/**
 * @deprecated, only available for SDK6 compatibility. This was a low level API that can
 * be replaced by any ethereum library on top of the provider
 */
export function signMessage(body: SignMessageRequest): Promise<SignMessageResponse>;
/**
 * @deprecated, only available for SDK6 compatibility. This was a low level API that can
 * be replaced by any ethereum library on top of the provider
 */
export function convertMessageToObject(body: ConvertMessageToObjectRequest): Promise<ConvertMessageToObjectResponse>;
export function sendAsync(body: SendAsyncRequest): Promise<SendAsyncResponse>;
/**
 * @deprecated, only available for SDK6 compatibility. This was a low level API that can
 * be replaced by any ethereum library on top of the provider
 */
export function getUserAccount(body: GetUserAccountRequest): Promise<GetUserAccountResponse>;


/**
  * Players
  */

// Function declaration section
export interface Player {
    userId: string;
}
export interface PlayersGetUserDataResponse {
    data?: UserData | undefined;
}
export interface PlayerListResponse {
    players: Player[];
}
export interface GetPlayerDataRequest {
    userId: string;
}
export interface GetPlayersInSceneRequest {
}
export interface GetConnectedPlayersRequest {
}

// Function declaration section
/**
* Returns data about a specific player, by id
* NOTE: To be deprecated after implementing foreign-entities and once the avatar scene uses SDK7
*/
export function getPlayerData(body: GetPlayerDataRequest): Promise<PlayersGetUserDataResponse>;
/**
 * Returns a list of all the ids of players who are currently standing within the parcels of the scene
 * NOTE: To be deprecated after implementing foreign-entities and once the avatar scene uses SDK7
 */
export function getPlayersInScene(body: GetPlayersInSceneRequest): Promise<PlayerListResponse>;
/**
 * Returns a list of all the ids of players who are currently connected to the same server and grouped together
 * NOTE: To be deprecated after implementing foreign-entities and once the avatar scene uses SDK7
 */
export function getConnectedPlayers(body: GetConnectedPlayersRequest): Promise<PlayerListResponse>;


/**
  * PortableExperiences
  */

export interface KillRequest {
    pid: string;
}
export interface KillResponse {
    status: boolean;
}
export interface SpawnRequest {
    pid?: string | undefined;
    ens?: string | undefined;
}
export interface SpawnResponse {
    pid: string;
    parentCid: string;
    name: string;
    ens?: string | undefined;
}
export interface PxRequest {
    pid: string;
}
export interface GetPortableExperiencesLoadedRequest {
}
export interface GetPortableExperiencesLoadedResponse {
    loaded: SpawnResponse[];
}
export interface ExitRequest {
}
export interface ExitResponse {
    status: boolean;
}

// Function declaration section
/**
* Spawns a new portable experience that is detached from the current scene.
* Spawned portable experiences can only be controlled by 1) the user (from the UI)
* and 2) from the parent scene. If the parent scene gets unloaded i.e. by distance,
* once the player re-loads the parent it will inherit the children portable experiences
* to gain control over them.
*/
export function spawn(body: SpawnRequest): Promise<SpawnResponse>;
/**
 * Kill a child portable experience, this method only works if the child was
 * spawned by the same process trying to kill it.
 */
export function kill(body: KillRequest): Promise<KillResponse>;
/**
 * Kill the current scene if the current scene is a portable experience. Other
 * kind of scenes are not allowed to finish their programs like portable experiences.
 */
export function exit(body: ExitRequest): Promise<ExitResponse>;
/**
 * Gets a list of running portable experiences for the current user. Be mindful
 * about the performance penalty of calling this function all frames.
 */
export function getPortableExperiencesLoaded(body: GetPortableExperiencesLoadedRequest): Promise<GetPortableExperiencesLoadedResponse>;


/**
  * RestrictedActions
  */

// Function declaration section
export interface MovePlayerToRequest {
    newRelativePosition: Vector3 | undefined;
    cameraTarget?: Vector3 | undefined;
}
export interface TeleportToRequest {
    worldCoordinates: Vector2 | undefined;
}
export interface TriggerEmoteRequest {
    predefinedEmote: string;
}
export interface ChangeRealmRequest {
    realm: string;
    message?: string | undefined;
}
export interface OpenExternalUrlRequest {
    url: string;
}
export interface OpenNftDialogRequest {
    urn: string;
}
export interface UnblockPointerRequest {
}
export interface CommsAdapterRequest {
    connectionString: string;
}
export interface TriggerSceneEmoteRequest {
    src: string;
    loop?: boolean | undefined;
}
export interface SuccessResponse {
    success: boolean;
}
export interface TriggerEmoteResponse {
}
export interface MovePlayerToResponse {
}
export interface TeleportToResponse {
}

// Function declaration section
/** MovePlayerTo will move the player in a position relative to the current scene */
export function movePlayerTo(body: MovePlayerToRequest): Promise<MovePlayerToResponse>;
/** TeleportTo will move the user to the specified world LAND parcel coordinates */
export function teleportTo(body: TeleportToRequest): Promise<TeleportToResponse>;
/** TriggerEmote will trigger an emote in this current user */
export function triggerEmote(body: TriggerEmoteRequest): Promise<TriggerEmoteResponse>;
/** ChangeRealm prompts the user to change to a specific realm */
export function changeRealm(body: ChangeRealmRequest): Promise<SuccessResponse>;
/** OpenExternalUrl prompts the user to open an external link */
export function openExternalUrl(body: OpenExternalUrlRequest): Promise<SuccessResponse>;
/** OpenNftDialog opens an NFT dialog. */
export function openNftDialog(body: OpenNftDialogRequest): Promise<SuccessResponse>;
/**
 * Asks the explorer to connect to other communications adapter, this feature
 * can be used to join private game servers
 */
export function setCommunicationsAdapter(body: CommsAdapterRequest): Promise<SuccessResponse>;
/** TriggerSceneEmote will trigger an scene emote file in this current user */
export function triggerSceneEmote(body: TriggerSceneEmoteRequest): Promise<SuccessResponse>;


/**
  * Runtime
  */

// Function declaration section
export interface RealmInfo {
    baseUrl: string;
    realmName: string;
    networkId: number;
    commsAdapter: string;
    isPreview: boolean;
}
export interface GetRealmResponse {
    realmInfo?: RealmInfo | undefined;
}
export interface GetWorldTimeResponse {
    seconds: number;
}
export interface GetRealmRequest {
}
export interface GetWorldTimeRequest {
}
export interface ReadFileRequest {
    /** name of the deployed file */
    fileName: string;
}
export interface ReadFileResponse {
    /** contents of the file */
    content: Uint8Array;
    /** deployed hash/CID */
    hash: string;
}
export interface CurrentSceneEntityRequest {
}
export interface CurrentSceneEntityResponse {
    /** this is either the entityId or the full URN of the scene that is running */
    urn: string;
    /** contents of the deployed entities */
    content: ContentMapping[];
    /** JSON serialization of the entity.metadata field */
    metadataJson: string;
    /** baseUrl used to resolve all content files */
    baseUrl: string;
}
export interface GetExplorerInformationRequest {
}
export interface GetExplorerInformationResponse {
    /** the agent that current explorer is identified as */
    agent: string;
    /** options: "desktop", "mobile", "vr", "web" */
    platform: string;
    /** custom configurations set in the explorer */
    configurations: {
        [key: string]: string;
    };
}
export interface GetExplorerInformationResponse_ConfigurationsEntry {
    key: string;
    value: string;
}

// Function declaration section
/** Provides information about the current realm */
export function getRealm(body: GetRealmRequest): Promise<GetRealmResponse>;
/**
 * Provides information about the Decentraland Time, which is coordinated
 * across players.
 */
export function getWorldTime(body: GetWorldTimeRequest): Promise<GetWorldTimeResponse>;
/**
 * Returns the file content of a deployed asset. If the file doesn't
 * exist or cannot be retrieved, the RPC call throws an error.
 * This method is called to load any assets deployed among the scene,
 * runtime may cache this response much more than the provided "fetch" function.
 */
export function readFile(body: ReadFileRequest): Promise<ReadFileResponse>;
/** Returns information about the current scene. This is the replacement of GetBootstrapData */
export function getSceneInformation(body: CurrentSceneEntityRequest): Promise<CurrentSceneEntityResponse>;
/** Provides information about the explorer */
export function getExplorerInformation(body: GetExplorerInformationRequest): Promise<GetExplorerInformationResponse>;


/**
  * Scene
  */

// Function declaration section
export interface GetSceneRequest {
}
export interface GetSceneResponse {
    cid: string;
    metadata: string;
    baseUrl: string;
    contents: ContentMapping[];
}

// Function declaration section
export function getSceneInfo(body: GetSceneRequest): Promise<GetSceneResponse>;

/**
  * SignedFetch
  */

export interface FlatFetchInit {
    method?: string | undefined;
    body?: string | undefined;
    headers: {
        [key: string]: string;
    };
}
export interface FlatFetchInit_HeadersEntry {
    key: string;
    value: string;
}
export interface FlatFetchResponse {
    ok: boolean;
    status: number;
    statusText: string;
    headers: {
        [key: string]: string;
    };
    body: string;
}
export interface FlatFetchResponse_HeadersEntry {
    key: string;
    value: string;
}
export interface SignedFetchRequest {
    url: string;
    init?: FlatFetchInit | undefined;
}
export interface GetHeadersResponse {
    headers: {
        [key: string]: string;
    };
}
export interface GetHeadersResponse_HeadersEntry {
    key: string;
    value: string;
}

// Function declaration section
/**
* SignedFetch is used to authenticate JSON requests in name of the users,
* a special scoped signature is generated following the https://adr.decentraland.org/adr/ADR-44
*/
export function signedFetch(body: SignedFetchRequest): Promise<FlatFetchResponse>;
export function getHeaders(body: SignedFetchRequest): Promise<GetHeadersResponse>;


/**
  * Testing
  */


export interface TestResult {
    name: string;
    ok: boolean;
    error?: string | undefined;
    stack?: string | undefined;
    /** how many ADR-148 ticks were spent running this test */
    totalFrames: number;
    /** total time in seconds spent running this test */
    totalTime: number;
}
export interface TestResultResponse {
}
export interface TestPlan {
    tests: TestPlan_TestPlanEntry[];
}
export interface TestPlan_TestPlanEntry {
    name: string;
}
export interface TestPlanResponse {
}
export interface SetCameraTransformTestCommand {
    position: SetCameraTransformTestCommand_Vector3 | undefined;
    rotation: SetCameraTransformTestCommand_Quaternion | undefined;
}
export interface SetCameraTransformTestCommand_Vector3 {
    x: number;
    y: number;
    z: number;
}
export interface SetCameraTransformTestCommand_Quaternion {
    x: number;
    y: number;
    z: number;
    w: number;
}
export interface SetCameraTransformTestCommandResponse {
}

// Function declaration section
/** sends a test result to the test runner */
export function logTestResult(body: TestResult): Promise<TestResultResponse>;
/** send a list of all planned tests to the test runner */
export function plan(body: TestPlan): Promise<TestPlanResponse>;
/** sets the camera position and rotation in the engine */
export function setCameraTransform(body: SetCameraTransformTestCommand): Promise<SetCameraTransformTestCommandResponse>;


/**
  * UserActionModule
  */


export interface RequestTeleportRequest {
    destination: string;
}
export interface RequestTeleportResponse {
}

// Function declaration section
/** @deprecated, only available for SDK6 compatibility. Use RestrictedActions/TeleportTo */
export function requestTeleport(body: RequestTeleportRequest): Promise<RequestTeleportResponse>;


/**
  * UserIdentity
  */


export interface Snapshots {
    face256: string;
    body: string;
}
export interface AvatarForUserData {
    bodyShape: string;
    skinColor: string;
    hairColor: string;
    eyeColor: string;
    wearables: string[];
    snapshots: Snapshots | undefined;
}
export interface UserData {
    displayName: string;
    publicKey?: string | undefined;
    hasConnectedWeb3: boolean;
    userId: string;
    version: number;
    avatar: AvatarForUserData | undefined;
}

// Function declaration section
export interface GetUserDataRequest {
}
export interface GetUserDataResponse {
    data?: UserData | undefined;
}
export interface GetUserPublicKeyRequest {
}
export interface GetUserPublicKeyResponse {
    address?: string | undefined;
}

// Function declaration section
/** @deprecated, only available for SDK6 compatibility. UseGetUserData */
export function getUserPublicKey(body: GetUserPublicKeyRequest): Promise<GetUserPublicKeyResponse>;
export function getUserData(body: GetUserDataRequest): Promise<GetUserDataResponse>;