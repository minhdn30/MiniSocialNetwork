using System.Text;
using System.Text.Json;
using CloudM.Infrastructure.Models;
using System.Security.Cryptography;
using static CloudM.Domain.Exceptions.CustomExceptions;

namespace CloudM.Application.Services.PostServices
{
    public static class PostFeedCursorTokenSerializer
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public static string Serialize(PostFeedCursorModel cursor, string signingKey)
        {
            if (cursor == null ||
                cursor.SnapshotAt == default ||
                string.IsNullOrWhiteSpace(cursor.ProfileKey) ||
                cursor.SessionSeed == 0 ||
                !cursor.HasPosition ||
                cursor.HasPartialWindowCursor)
            {
                throw new ArgumentException("Feed cursor is invalid.", nameof(cursor));
            }

            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new ArgumentException("Feed cursor signing key is required.", nameof(signingKey));
            }

            var json = JsonSerializer.Serialize(cursor, SerializerOptions);
            var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(json));
            var signature = Base64UrlEncode(CreateSignature(payload, signingKey));
            return $"{payload}.{signature}";
        }

        public static PostFeedCursorModel Deserialize(string token, string signingKey)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new BadRequestException("Invalid feed cursor.");
            }

            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new BadRequestException("Invalid feed cursor.");
            }

            try
            {
                var tokenParts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
                if (tokenParts.Length != 2)
                {
                    throw new BadRequestException("Invalid feed cursor.");
                }

                var payload = tokenParts[0];
                var providedSignature = Base64UrlDecode(tokenParts[1]);
                var expectedSignature = CreateSignature(payload, signingKey);

                if (providedSignature.Length != expectedSignature.Length ||
                    !CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
                {
                    throw new BadRequestException("Invalid feed cursor.");
                }

                var json = Encoding.UTF8.GetString(Base64UrlDecode(payload));
                var cursor = JsonSerializer.Deserialize<PostFeedCursorModel>(json, SerializerOptions);

                if (cursor == null ||
                    cursor.SnapshotAt == default ||
                    string.IsNullOrWhiteSpace(cursor.ProfileKey) ||
                    cursor.SessionSeed == 0 ||
                    !cursor.HasPosition ||
                    cursor.HasPartialPosition && !cursor.HasPosition ||
                    cursor.HasPartialWindowCursor)
                {
                    throw new BadRequestException("Invalid feed cursor.");
                }

                return cursor;
            }
            catch (BadRequestException)
            {
                throw;
            }
            catch
            {
                throw new BadRequestException("Invalid feed cursor.");
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static byte[] CreateSignature(string payload, string signingKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        }

        private static byte[] Base64UrlDecode(string token)
        {
            var padded = token
                .Replace('-', '+')
                .Replace('_', '/');

            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
                case 0:
                    break;
                default:
                    throw new FormatException("Invalid base64url token.");
            }

            return Convert.FromBase64String(padded);
        }
    }
}
