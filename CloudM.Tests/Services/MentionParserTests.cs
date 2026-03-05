using CloudM.Application.Helpers.ValidationHelpers;
using FluentAssertions;

namespace CloudM.Tests.Services
{
    public class MentionParserTests
    {
        [Fact]
        public void ExtractTokens_PlainMentionWithHyphenContinuation_ShouldNotMatchPartialMention()
        {
            // Arrange
            var content = "hello @chanh1-zz";

            // Act
            var tokens = MentionParser.ExtractTokens(content);

            // Assert
            tokens.Should().BeEmpty();
        }

        [Fact]
        public void ExtractTokens_PlainMentionWithBoundary_ShouldMatch()
        {
            // Arrange
            var content = "hello @chanh1, nice to meet you";

            // Act
            var tokens = MentionParser.ExtractTokens(content);

            // Assert
            tokens.Should().HaveCount(1);
            tokens[0].IsCanonical.Should().BeFalse();
            tokens[0].Username.Should().Be("chanh1");
            tokens[0].RawText.Should().Be("@chanh1");
        }

        [Fact]
        public void ExtractTokens_CanonicalMention_ShouldMatch()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var content = $"hello @[chanh1]({accountId})";

            // Act
            var tokens = MentionParser.ExtractTokens(content);

            // Assert
            tokens.Should().HaveCount(1);
            tokens[0].IsCanonical.Should().BeTrue();
            tokens[0].Username.Should().Be("chanh1");
            tokens[0].AccountId.Should().Be(accountId);
        }
    }
}
