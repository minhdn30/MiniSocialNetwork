namespace SocialNetwork.Application.DTOs.AuthDTOs
{
    public class ExternalLoginStartResponse
    {
        public bool RequiresProfileCompletion { get; set; }
        public LoginResponse? Login { get; set; }
        public ExternalProfilePrefillResponse? Profile { get; set; }
    }

    public class ExternalProfilePrefillResponse
    {
        public string Provider { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SuggestedUsername { get; set; } = string.Empty;
        public string SuggestedFullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
    }
}
