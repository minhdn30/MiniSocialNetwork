namespace CloudM.Application.Services.NotificationServices
{
    public class NotificationOptions
    {
        public bool EnableWorker { get; set; } = false;
        public int OutboxPollIntervalMs { get; set; } = 1000;
        public int OutboxBatchSize { get; set; } = 50;
        public int OutboxLockSeconds { get; set; } = 30;
        public int MaxRetryAttempts { get; set; } = 8;
        public int RetryBaseDelayMs { get; set; } = 500;
        public int RetryMaxDelayMs { get; set; } = 30000;
        public int RetentionDays { get; set; } = 14;
    }
}
