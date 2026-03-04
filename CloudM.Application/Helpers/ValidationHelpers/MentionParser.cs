using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CloudM.Application.Helpers.ValidationHelpers
{
    public static class MentionParser
    {
        private const int MaxMentionUsernameLength = 30;

        private static readonly Regex CanonicalMentionRegex = new(
            @"@\[(?<username>[A-Za-z0-9._]{1,30})\]\((?<accountId>[0-9a-fA-F-]{36})\)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PlainMentionRegex = new(
            @"(?<![A-Za-z0-9._])@(?<username>[A-Za-z0-9._]{1,30})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public sealed class MentionToken
        {
            public int StartIndex { get; init; }
            public int Length { get; init; }
            public bool IsCanonical { get; init; }
            public string Username { get; init; } = string.Empty;
            public Guid? AccountId { get; init; }
            public string RawText { get; init; } = string.Empty;
        }

        public static List<MentionToken> ExtractTokens(string? content)
        {
            var safeContent = content ?? string.Empty;
            if (safeContent.Length == 0)
            {
                return new List<MentionToken>();
            }

            var tokens = new List<MentionToken>();

            var occupiedRanges = new List<(int Start, int End)>();
            var canonicalMatches = CanonicalMentionRegex.Matches(safeContent);
            foreach (Match match in canonicalMatches)
            {
                if (!match.Success)
                {
                    continue;
                }

                var username = NormalizeUsername(match.Groups["username"].Value);
                if (string.IsNullOrWhiteSpace(username))
                {
                    continue;
                }

                if (!Guid.TryParse(match.Groups["accountId"].Value, out var accountId))
                {
                    continue;
                }

                tokens.Add(new MentionToken
                {
                    StartIndex = match.Index,
                    Length = match.Length,
                    IsCanonical = true,
                    Username = username,
                    AccountId = accountId,
                    RawText = match.Value
                });

                occupiedRanges.Add((match.Index, match.Index + match.Length));
            }

            var plainMatches = PlainMentionRegex.Matches(safeContent);
            foreach (Match match in plainMatches)
            {
                if (!match.Success)
                {
                    continue;
                }

                var start = match.Index;
                var end = match.Index + match.Length;
                if (IsInsideRanges(start, end, occupiedRanges))
                {
                    continue;
                }

                var username = NormalizeUsername(match.Groups["username"].Value);
                if (string.IsNullOrWhiteSpace(username))
                {
                    continue;
                }

                tokens.Add(new MentionToken
                {
                    StartIndex = match.Index,
                    Length = match.Length,
                    IsCanonical = false,
                    Username = username,
                    AccountId = null,
                    RawText = match.Value
                });
            }

            tokens.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));
            return tokens;
        }

        public static string NormalizeUsername(string? username)
        {
            var normalized = (username ?? string.Empty).Trim();
            if (normalized.StartsWith("@"))
            {
                normalized = normalized.Substring(1);
            }

            if (normalized.Length > MaxMentionUsernameLength)
            {
                return string.Empty;
            }

            return normalized;
        }

        public static string BuildCanonicalMentionText(string username, Guid accountId)
        {
            var normalizedUsername = NormalizeUsername(username);
            if (string.IsNullOrWhiteSpace(normalizedUsername))
            {
                return string.Empty;
            }

            return $"@[{normalizedUsername}]({accountId})";
        }

        public static string BuildPlainMentionText(string username)
        {
            var normalizedUsername = NormalizeUsername(username);
            if (string.IsNullOrWhiteSpace(normalizedUsername))
            {
                return string.Empty;
            }

            return $"@{normalizedUsername}";
        }

        private static bool IsInsideRanges(int start, int end, List<(int Start, int End)> ranges)
        {
            foreach (var range in ranges)
            {
                if (start >= range.Start && end <= range.End)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
