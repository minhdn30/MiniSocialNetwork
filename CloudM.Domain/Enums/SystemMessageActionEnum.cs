namespace CloudM.Domain.Enums
{
    /// Action type for system messages in group chat.
    public enum SystemMessageActionEnum
    {
        MemberAdded = 1,      // Add members to the group
        MemberLeft = 2,       // Member leaves the group
        MemberKicked = 3,     // Member kicked
        GroupRenamed = 4,     // Rename group
        AdminGranted = 5,     // Grant admin privileges
        AdminRevoked = 6,     // Revoke admin privileges
        GroupCreated = 7,     // Create new group
        MemberNicknameUpdated = 8, // Update member nickname
        ConversationThemeUpdated = 9, // Update conversation theme
        MessagePinned = 10,          // Pin a message
        MessageUnpinned = 11,         // Unpin a message
        GroupAvatarUpdated = 12,      // Update group avatar
        GroupInfoUpdated = 13,        // Update group name/avatar
        OwnerTransferred = 14         // Transfer group owner
    }
}
