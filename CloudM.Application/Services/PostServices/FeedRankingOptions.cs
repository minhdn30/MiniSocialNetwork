using CloudM.Infrastructure.Models;

namespace CloudM.Application.Services.PostServices
{
    public class FeedRankingOptions
    {
        public string ActiveProfile { get; set; } = FeedRankingProfileModel.DefaultProfileKey;
        public string CursorSigningKey { get; set; } = string.Empty;
        public Dictionary<string, FeedRankingProfileModel> Profiles { get; set; } =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [FeedRankingProfileModel.DefaultProfileKey] = new FeedRankingProfileModel()
            };

        public FeedRankingOptions Normalize()
        {
            var normalizedProfiles = new Dictionary<string, FeedRankingProfileModel>(StringComparer.OrdinalIgnoreCase);

            if (Profiles != null)
            {
                foreach (var entry in Profiles)
                {
                    var profileKey = string.IsNullOrWhiteSpace(entry.Key)
                        ? FeedRankingProfileModel.DefaultProfileKey
                        : entry.Key.Trim();

                    var profile = (entry.Value ?? new FeedRankingProfileModel())
                        .Copy()
                        .Normalize(profileKey);

                    normalizedProfiles[profileKey] = profile;
                }
            }

            if (!normalizedProfiles.ContainsKey(FeedRankingProfileModel.DefaultProfileKey))
            {
                normalizedProfiles[FeedRankingProfileModel.DefaultProfileKey] =
                    new FeedRankingProfileModel().Normalize(FeedRankingProfileModel.DefaultProfileKey);
            }

            ActiveProfile = string.IsNullOrWhiteSpace(ActiveProfile)
                ? FeedRankingProfileModel.DefaultProfileKey
                : ActiveProfile.Trim();

            if (!normalizedProfiles.ContainsKey(ActiveProfile))
            {
                ActiveProfile = FeedRankingProfileModel.DefaultProfileKey;
            }

            CursorSigningKey = string.IsNullOrWhiteSpace(CursorSigningKey)
                ? string.Empty
                : CursorSigningKey.Trim();

            Profiles = normalizedProfiles;
            return this;
        }

        public FeedRankingProfileModel ResolveActiveProfile()
        {
            Normalize();
            return Profiles[ActiveProfile].Copy().Normalize(ActiveProfile);
        }

        public bool TryResolveProfile(string? profileKey, out FeedRankingProfileModel profile)
        {
            Normalize();

            var resolvedKey = string.IsNullOrWhiteSpace(profileKey)
                ? ActiveProfile
                : profileKey.Trim();

            if (Profiles.TryGetValue(resolvedKey, out var existingProfile))
            {
                profile = existingProfile.Copy().Normalize(resolvedKey);
                return true;
            }

            profile = new FeedRankingProfileModel().Normalize();
            return false;
        }
    }
}
