namespace CloudM.Application.Helpers
{
    public static class LanguagePreferenceHelper
    {
        public const string DefaultLanguage = "en";

        private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
        {
            "en",
            "vi"
        };

        public static string Normalize(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return SupportedLanguages.Contains(normalized)
                ? normalized
                : DefaultLanguage;
        }

        public static bool IsSupported(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return SupportedLanguages.Contains(normalized);
        }
    }
}
