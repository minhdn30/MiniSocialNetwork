using System.Security.Cryptography;
using System.Text;

namespace SocialNetwork.Application.Helpers
{
    public static class StringHelper
    {
        private static readonly char[] Base62Chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

        public static string GeneratePostCode(int length = 10)
        {
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                sb.Append(Base62Chars[RandomNumberGenerator.GetInt32(Base62Chars.Length)]);
            }
            return sb.ToString();
        }
    }
}
