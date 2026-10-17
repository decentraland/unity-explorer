namespace DCL.SceneRestrictionBusController.SceneRestriction
{
    public struct SceneRestriction
    {
        public SceneRestrictions Type { get; set; }
        public SceneRestrictionsAction Action { get; set; }

        public static SceneRestriction CreateCameraLocked(SceneRestrictionsAction action) =>
            new()
            {
                Type = SceneRestrictions.CameraLocked,
                Action = action,
            };

        public static SceneRestriction CreateAvatarHidden(SceneRestrictionsAction action) =>
            new()
            {
                Type = SceneRestrictions.AvatarHidden,
                Action = action,
            };

        public static SceneRestriction CreateAvatarMovementsBlocked(SceneRestrictionsAction action) =>
            new()
            {
                Type = SceneRestrictions.AvatarMovementsBlocked,
                Action = action,
            };

        public static SceneRestriction CreatePassportCannotBeOpened(SceneRestrictionsAction action) =>
            new()
            {
                Type = SceneRestrictions.PassportCannotBeOpened,
                Action = action,
            };

        public static SceneRestriction CreateExperiencesBlocked(SceneRestrictionsAction action) =>
            new()
            {
                Type = SceneRestrictions.ExperiencesBlocked,
                Action = action,
            };

        public static SceneRestriction CreateSkyboxTimeUILocked(SceneRestrictionsAction action) =>
            new ()
            {
                Type = SceneRestrictions.SkyboxTimeUiBlocked,
                Action = action,
            };

        public static SceneRestriction CreateNearbyVoiceChatBlocked(SceneRestrictionsAction action) =>
            new ()
            {
                Type = SceneRestrictions.NearbyVoiceChatBlocked,
                Action = action,
            };
    }

    public enum SceneRestrictions
    {
        CameraLocked,
        AvatarHidden,
        AvatarMovementsBlocked,
        PassportCannotBeOpened,
        ExperiencesBlocked,
        SkyboxTimeUiBlocked,
        NearbyVoiceChatBlocked,
    }

    public enum SceneRestrictionsAction
    {
        Applied,
        Removed,
    }
}
