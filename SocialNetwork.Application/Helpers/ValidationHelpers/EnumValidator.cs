using SocialNetwork.Application.Exceptions;
using System;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Helpers.ValidationHelpers
{
    public static class EnumValidator
    {
        public static void ValidateEnum<TEnum>(int value, string? errorMessage = null) where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new BadRequestException(errorMessage ?? $"Invalid {typeof(TEnum).Name} value.");
            }
        }

        public static void ValidateEnumIfHasValue<TEnum>(int? value, string? errorMessage = null) where TEnum : struct, Enum
        {
            if (value.HasValue && !Enum.IsDefined(typeof(TEnum), value.Value))
            {
                throw new BadRequestException(errorMessage ?? $"Invalid {typeof(TEnum).Name} value.");
            }
        }

        public static void ValidateEnum<TEnum>(TEnum value, string? errorMessage = null) where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new BadRequestException(errorMessage ?? $"Invalid {typeof(TEnum).Name} value.");
            }
        }

        public static void ValidateEnumIfHasValue<TEnum>(TEnum? value, string? errorMessage = null) where TEnum : struct, Enum
        {
            if (value.HasValue && !Enum.IsDefined(typeof(TEnum), value.Value))
            {
                throw new BadRequestException(errorMessage ?? $"Invalid {typeof(TEnum).Name} value.");
            }
        }
    }
}
