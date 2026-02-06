using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Helpers
{
    public static class StringHelper
    {
        private static readonly char[] Base62Chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

        public static string GeneratePostCode(int length = 10)
        {
            var random = new Random();
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                sb.Append(Base62Chars[random.Next(Base62Chars.Length)]);
            }
            return sb.ToString();
        }
    }
}
