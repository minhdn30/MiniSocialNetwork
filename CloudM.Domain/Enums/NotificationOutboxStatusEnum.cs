namespace CloudM.Domain.Enums
{
    public enum NotificationOutboxStatusEnum
    {
        Pending = 0,
        Processing = 1,
        Processed = 2,
        DeadLetter = 3
    }
}
