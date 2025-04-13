namespace DCL.Chat
{
    public enum ChatUserState
    {
        CONNECTED, //Online friends and other users that are not blocked if both users have ALL set in privacy setting.
        BLOCKED_BY_OWN_USER, //Own user blocked the other user
        PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER, //Own user has privacy settings set to ONLY FRIENDS
        PRIVATE_MESSAGES_BLOCKED, //The other user has its privacy settings set to ONLY FRIENDS
        DISCONNECTED //The other user is either offline or has blocked the own user.
    }
} 