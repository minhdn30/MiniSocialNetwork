namespace CloudM.Application.Services.PresenceServices
{
    public class OnlinePresenceOptions
    {
        public int HeartbeatTtlSeconds { get; set; } = 120;
        public int OfflineGraceSeconds { get; set; } = 60;
        public int OfflineLockSeconds { get; set; } = 10;
        public int WorkerIntervalSeconds { get; set; } = 3;
        public int WorkerBatchSize { get; set; } = 100;
        public int SnapshotMaxAccountIds { get; set; } = 200;
        public int SnapshotRateLimitWindowSeconds { get; set; } = 30;
        public int SnapshotRateLimitMaxRequests { get; set; } = 60;

        public OnlinePresenceOptions Normalize()
        {
            HeartbeatTtlSeconds = HeartbeatTtlSeconds <= 0 ? 120 : HeartbeatTtlSeconds;
            OfflineGraceSeconds = OfflineGraceSeconds < 0 ? 60 : OfflineGraceSeconds;
            OfflineLockSeconds = OfflineLockSeconds <= 0 ? 10 : OfflineLockSeconds;
            WorkerIntervalSeconds = WorkerIntervalSeconds <= 0 ? 3 : WorkerIntervalSeconds;
            WorkerBatchSize = WorkerBatchSize <= 0 ? 100 : WorkerBatchSize;
            SnapshotMaxAccountIds = SnapshotMaxAccountIds <= 0 ? 200 : SnapshotMaxAccountIds;
            SnapshotRateLimitWindowSeconds = SnapshotRateLimitWindowSeconds <= 0 ? 30 : SnapshotRateLimitWindowSeconds;
            SnapshotRateLimitMaxRequests = SnapshotRateLimitMaxRequests <= 0 ? 60 : SnapshotRateLimitMaxRequests;
            return this;
        }
    }
}
