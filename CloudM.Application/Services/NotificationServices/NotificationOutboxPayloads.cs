using CloudM.Domain.Enums;

namespace CloudM.Application.Services.NotificationServices
{
    public class NotificationAggregateChangedPayload
    {
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

    public class NotificationTargetUnavailablePayload
    {
        public NotificationTypeEnum Type { get; set; }
        public string? AggregateKey { get; set; }
        public NotificationTargetKindEnum TargetKind { get; set; }
        public Guid TargetId { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }

    public class NotificationActorSnapshot
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }

    public enum NotificationProjectionActionEnum
    {
        None = 0,
        Upsert = 1,
        Remove = 2
    }

    public class NotificationProjectionResult
    {
        public NotificationProjectionActionEnum Action { get; set; }
        public Guid RecipientId { get; set; }
        public Guid? NotificationId { get; set; }
        public bool AffectsUnread { get; set; }
        public Guid EventId { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }
}
