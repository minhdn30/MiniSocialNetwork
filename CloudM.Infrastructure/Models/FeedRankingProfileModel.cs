namespace CloudM.Infrastructure.Models
{
    public class FeedRankingProfileModel
    {
        public const string DefaultProfileKey = "balanced-v1";

        public string ProfileKey { get; set; } = DefaultProfileKey;
        public int CandidateMultiplier { get; set; } = 12;
        public int CandidateMinCount { get; set; } = 120;
        public int CandidateMaxCount { get; set; } = 600;
        public int RecentEngagementWindowDays { get; set; } = 7;
        public int AffinityHotWindowDays { get; set; } = 7;
        public int AffinityWarmWindowDays { get; set; } = 30;
        public int FreshnessDay1Hours { get; set; } = 24;
        public int FreshnessDay3Hours { get; set; } = 72;
        public int FreshnessDay7Hours { get; set; } = 168;
        public int FreshnessDay14Hours { get; set; } = 336;
        public int FreshnessDay30Hours { get; set; } = 720;
        public decimal FreshnessDay1Score { get; set; } = 60m;
        public decimal FreshnessDay3Score { get; set; } = 42m;
        public decimal FreshnessDay7Score { get; set; } = 30m;
        public decimal FreshnessDay14Score { get; set; } = 18m;
        public decimal FreshnessDay30Score { get; set; } = 10m;
        public decimal FreshnessOlderScore { get; set; } = 0m;
        public int ReactCountCap { get; set; } = 12;
        public int RootCommentCountCap { get; set; } = 8;
        public int ReplyCountCap { get; set; } = 12;
        public decimal ReactWeight { get; set; } = 5m;
        public decimal RootCommentWeight { get; set; } = 9m;
        public decimal ReplyWeight { get; set; } = 4m;
        public decimal FollowBonus { get; set; } = 18m;
        public decimal SelfFallbackBonus { get; set; } = 4m;
        public decimal DiscoverBonus { get; set; } = 6m;
        public decimal AffinityHotBonus { get; set; } = 14m;
        public decimal AffinityWarmBonus { get; set; } = 6m;
        public int JitterBucketCount { get; set; } = 7;

        public FeedRankingProfileModel Normalize(string? profileKey = null)
        {
            ProfileKey = string.IsNullOrWhiteSpace(profileKey)
                ? (string.IsNullOrWhiteSpace(ProfileKey) ? DefaultProfileKey : ProfileKey.Trim())
                : profileKey.Trim();

            CandidateMultiplier = CandidateMultiplier <= 0 ? 12 : CandidateMultiplier;
            CandidateMinCount = CandidateMinCount <= 0 ? 120 : CandidateMinCount;
            CandidateMaxCount = CandidateMaxCount <= 0 ? 600 : CandidateMaxCount;
            if (CandidateMaxCount < CandidateMinCount)
            {
                CandidateMaxCount = CandidateMinCount;
            }

            RecentEngagementWindowDays = RecentEngagementWindowDays <= 0 ? 7 : RecentEngagementWindowDays;
            AffinityHotWindowDays = AffinityHotWindowDays <= 0 ? 7 : AffinityHotWindowDays;
            AffinityWarmWindowDays = AffinityWarmWindowDays < AffinityHotWindowDays ? 30 : AffinityWarmWindowDays;

            FreshnessDay1Hours = FreshnessDay1Hours <= 0 ? 24 : FreshnessDay1Hours;
            FreshnessDay3Hours = FreshnessDay3Hours < FreshnessDay1Hours ? 72 : FreshnessDay3Hours;
            FreshnessDay7Hours = FreshnessDay7Hours < FreshnessDay3Hours ? 168 : FreshnessDay7Hours;
            FreshnessDay14Hours = FreshnessDay14Hours < FreshnessDay7Hours ? 336 : FreshnessDay14Hours;
            FreshnessDay30Hours = FreshnessDay30Hours < FreshnessDay14Hours ? 720 : FreshnessDay30Hours;

            ReactCountCap = ReactCountCap < 0 ? 12 : ReactCountCap;
            RootCommentCountCap = RootCommentCountCap < 0 ? 8 : RootCommentCountCap;
            ReplyCountCap = ReplyCountCap < 0 ? 12 : ReplyCountCap;

            JitterBucketCount = JitterBucketCount <= 1 ? 1 : JitterBucketCount;
            if (JitterBucketCount % 2 == 0)
            {
                JitterBucketCount += 1;
            }

            return this;
        }

        public int ResolveCandidateLimit(int limit)
        {
            var normalizedLimit = limit <= 0 ? 10 : limit;
            var scaledLimit = normalizedLimit * CandidateMultiplier;
            var boundedByMin = Math.Max(scaledLimit, CandidateMinCount);
            return Math.Min(boundedByMin, CandidateMaxCount);
        }

        public FeedRankingProfileModel Copy()
        {
            return new FeedRankingProfileModel
            {
                ProfileKey = ProfileKey,
                CandidateMultiplier = CandidateMultiplier,
                CandidateMinCount = CandidateMinCount,
                CandidateMaxCount = CandidateMaxCount,
                RecentEngagementWindowDays = RecentEngagementWindowDays,
                AffinityHotWindowDays = AffinityHotWindowDays,
                AffinityWarmWindowDays = AffinityWarmWindowDays,
                FreshnessDay1Hours = FreshnessDay1Hours,
                FreshnessDay3Hours = FreshnessDay3Hours,
                FreshnessDay7Hours = FreshnessDay7Hours,
                FreshnessDay14Hours = FreshnessDay14Hours,
                FreshnessDay30Hours = FreshnessDay30Hours,
                FreshnessDay1Score = FreshnessDay1Score,
                FreshnessDay3Score = FreshnessDay3Score,
                FreshnessDay7Score = FreshnessDay7Score,
                FreshnessDay14Score = FreshnessDay14Score,
                FreshnessDay30Score = FreshnessDay30Score,
                FreshnessOlderScore = FreshnessOlderScore,
                ReactCountCap = ReactCountCap,
                RootCommentCountCap = RootCommentCountCap,
                ReplyCountCap = ReplyCountCap,
                ReactWeight = ReactWeight,
                RootCommentWeight = RootCommentWeight,
                ReplyWeight = ReplyWeight,
                FollowBonus = FollowBonus,
                SelfFallbackBonus = SelfFallbackBonus,
                DiscoverBonus = DiscoverBonus,
                AffinityHotBonus = AffinityHotBonus,
                AffinityWarmBonus = AffinityWarmBonus,
                JitterBucketCount = JitterBucketCount
            };
        }
    }
}
