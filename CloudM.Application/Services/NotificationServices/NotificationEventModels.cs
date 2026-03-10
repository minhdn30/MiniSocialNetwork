using CloudM.Domain.Enums;

namespace CloudM.Application.Services.NotificationServices
{
    public enum NotificationAggregateActionEnum
    {
        Upsert = 0,
        Deactivate = 1,
        DeactivateAll = 2
    }

    public static class NotificationOutboxEventTypes
    {
        public const string AggregateChanged = "notification.aggregate.changed";
        public const string TargetUnavailable = "notification.target.unavailable";
        public const string TargetUnavailableBroadcast = "notification.target.unavailable.broadcast";
    }

    public class NotificationAggregateEvent
    {
        public Guid RecipientId { get; set; }
        public NotificationAggregateActionEnum Action { get; set; }
        public NotificationTypeEnum Type { get; set; }
        public string AggregateKey { get; set; } = string.Empty;
        public NotificationSourceTypeEnum SourceType { get; set; }
        public Guid SourceId { get; set; }
        public Guid? ActorId { get; set; }
        public NotificationTargetKindEnum TargetKind { get; set; } = NotificationTargetKindEnum.None;
        public Guid? TargetId { get; set; }
        public bool KeepWhenEmpty { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }

    public class NotificationTargetUnavailableEvent
    {
        public Guid RecipientId { get; set; }
        public NotificationTypeEnum Type { get; set; }
        public string? AggregateKey { get; set; }
        public NotificationTargetKindEnum TargetKind { get; set; }
        public Guid TargetId { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }
}
