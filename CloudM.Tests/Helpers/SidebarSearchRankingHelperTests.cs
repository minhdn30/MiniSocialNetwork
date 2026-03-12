using FluentAssertions;
using CloudM.Infrastructure.Helpers;

namespace CloudM.Tests.Helpers
{
    public class SidebarSearchRankingHelperTests
    {
        [Fact]
        public void ShouldUseFuzzySimilarity_WhenKeywordHasExcessiveRepeatedCharacters_ReturnsFalse()
        {
            var result = SidebarSearchRankingHelper.ShouldUseFuzzySimilarity("chanhhhhhhhhhhhh");

            result.Should().BeFalse();
        }

        [Fact]
        public void ShouldUseFuzzySimilarity_WhenKeywordHasMinorTypo_ReturnsTrue()
        {
            var result = SidebarSearchRankingHelper.ShouldUseFuzzySimilarity("chanhh");

            result.Should().BeTrue();
        }

        [Fact]
        public void IsStrongMatch_WhenUsernameStartsWith_ReturnsTrue()
        {
            var result = SidebarSearchRankingHelper.IsStrongMatch(
                usernameExact: false,
                usernameStartsWith: true,
                fullNameStartsWith: false,
                fullNameWordStartsWith: false,
                usernameContains: false,
                fullNameContains: false);

            result.Should().BeTrue();
        }

        [Fact]
        public void IsFuzzyMatchEligible_WhenLengthDeltaIsTooLarge_ReturnsFalse()
        {
            var result = SidebarSearchRankingHelper.IsFuzzyMatchEligible(
                "chanh211004",
                15,
                0.33333334d);

            result.Should().BeFalse();
        }

        [Fact]
        public void IsFuzzyMatchEligible_WhenSimilarityAndLengthAreReasonable_ReturnsTrue()
        {
            var result = SidebarSearchRankingHelper.IsFuzzyMatchEligible(
                "chanh211004",
                11,
                0.33333334d);

            result.Should().BeTrue();
        }
    }
}
