namespace Unity.ECS.Components
{
    public static class SpecialEntityId
    {
        public const long SCENE_ROOT_ENTITY = 0;
        public const long PLAYER_ENTITY = 1;
        public const long CAMERA_ENTITY = 2;
        public const long INTERNAL_PLAYER_ENTITY_REPRESENTATION = 510;

        // To be deprecated soon
        public const long AVATAR_ENTITY_REFERENCE = 3;
        public const long AVATAR_POSITION_REFERENCE = 4;
        public const long FIRST_PERSON_CAMERA_ENTITY_REFERENCE = 5;
        public const long THIRD_PERSON_CAMERA_ENTITY_REFERENCE = 6;
    }
}
