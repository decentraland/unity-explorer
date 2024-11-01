namespace DCL.SceneRestrictionBusController.SceneRestriction
{
    public struct SceneRestriction
    {
        public SceneRestrictions Type { get; set; }
        public SceneRestrictionsAction Action { get; set; }

        public static SceneRestriction CreateCameraLocked(SceneRestrictionsAction action) =>
            new()
            {
                Type = SceneRestrictions.CAMERA_LOCKED,
                Action = action
            };

        public static SceneRestriction CreateAvatarHidden(SceneRestrictionsAction action) =>
            new()
            {
                Type = SceneRestrictions.AVATAR_HIDDEN,
                Action = action
            };

        public static SceneRestriction CreateAvatarMovementsBlocked(SceneRestrictionsAction action) =>
            new()
            {
                Type = SceneRestrictions.AVATAR_MOVEMENTS_BLOCKED,
                Action = action
            };

        public static SceneRestriction CreatePassportCannotBeOpened(SceneRestrictionsAction action) =>
            new()
            {
                Type = SceneRestrictions.PASSPORT_CANNOT_BE_OPENED,
                Action = action
            };

        public static SceneRestriction CreateExperiencesBlocked(SceneRestrictionsAction action) =>
            new()
            {
                Type = SceneRestrictions.EXPERIENCES_BLOCKED,
                Action = action
            };
    }

    public enum SceneRestrictions
    {
        CAMERA_LOCKED,
        AVATAR_HIDDEN,
        AVATAR_MOVEMENTS_BLOCKED,
        PASSPORT_CANNOT_BE_OPENED,
        EXPERIENCES_BLOCKED,
    }

    public enum SceneRestrictionsAction
    {
        APPLIED,
        REMOVED,
    }
}
