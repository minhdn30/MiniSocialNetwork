namespace CloudM.Infrastructure.Helpers
{
    internal static class SidebarSearchRankingHelper
    {
        internal const double FuzzySimilarityThreshold = 0.31d;
        internal const int MaxFuzzyQueryInflationDelta = 3;
        private const int MaxRepeatedCharacterRun = 3;

        internal static bool ShouldUseFuzzySimilarity(string keyword)
        {
            var normalizedKeyword = (keyword ?? string.Empty).Trim();
            if (normalizedKeyword.Length < 2)
            {
                return false;
            }

            return !HasExcessiveRepeatedCharacterRun(normalizedKeyword);
        }

        internal static bool IsStrongMatch(
            bool usernameExact,
            bool usernameStartsWith,
            bool fullNameStartsWith,
            bool fullNameWordStartsWith,
            bool usernameContains,
            bool fullNameContains)
        {
            return usernameExact ||
                usernameStartsWith ||
                fullNameStartsWith ||
                fullNameWordStartsWith ||
                usernameContains ||
                fullNameContains;
        }

        internal static bool IsFuzzyMatchEligible(
            string? candidateValue,
            int keywordLength,
            double similarity)
        {
            if (keywordLength <= 0 || similarity < FuzzySimilarityThreshold)
            {
                return false;
            }

            var candidateLength = (candidateValue ?? string.Empty).Trim().Length;
            if (candidateLength == 0)
            {
                return false;
            }

            return keywordLength - candidateLength <= MaxFuzzyQueryInflationDelta;
        }

        private static bool HasExcessiveRepeatedCharacterRun(string keyword)
        {
            var maxRun = 1;
            var currentRun = 1;

            for (var i = 1; i < keyword.Length; i += 1)
            {
                var currentChar = keyword[i];
                var previousChar = keyword[i - 1];

                if (!char.IsLetterOrDigit(currentChar) || !char.IsLetterOrDigit(previousChar))
                {
                    currentRun = 1;
                    continue;
                }

                if (char.ToLowerInvariant(currentChar) == char.ToLowerInvariant(previousChar))
                {
                    currentRun += 1;
                    if (currentRun > maxRun)
                    {
                        maxRun = currentRun;
                    }
                }
                else
                {
                    currentRun = 1;
                }
            }

            return maxRun > MaxRepeatedCharacterRun;
        }
    }
}
