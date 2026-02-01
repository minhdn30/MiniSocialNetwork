using SocialNetwork.Application.DTOs.PostMediaDTOs;
using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Validators
{
    public static class MediaCropValidator
    {
        private const float RATIO_TOLERANCE = 0.05f; // 5%

        // ENTRY POINT
        public static void Validate(
            PostMediaCropInfoRequest crop,
            AspectRatioEnum feedAspectRatio
        )
        {
            if (crop == null)
                return;

            ValidateCompleteness(crop);
            ValidateRange(crop);

            if (feedAspectRatio != AspectRatioEnum.Original)
            {
                ValidateAspectRatioCompatibility(crop, feedAspectRatio);
            }
        }

        // VALIDATE: enough field
        private static void ValidateCompleteness(PostMediaCropInfoRequest crop)
        {
            bool any =
                crop.CropX.HasValue ||
                crop.CropY.HasValue ||
                crop.CropWidth.HasValue ||
                crop.CropHeight.HasValue;

            bool all =
                crop.CropX.HasValue &&
                crop.CropY.HasValue &&
                crop.CropWidth.HasValue &&
                crop.CropHeight.HasValue;

            if (any && !all)
            {
                throw new BadRequestException(
                    "Crop must include CropX, CropY, CropWidth and CropHeight."
                );
            }
        }

        // located in the photo
        private static void ValidateRange(PostMediaCropInfoRequest crop)
        {
            if (!IsBetween0And1(crop.CropX!.Value) ||
                !IsBetween0And1(crop.CropY!.Value) ||
                !IsBetween0And1(crop.CropWidth!.Value) ||
                !IsBetween0And1(crop.CropHeight!.Value))
            {
                throw new BadRequestException("Crop values must be between 0 and 1.");
            }

            if (crop.CropX + crop.CropWidth > 1 ||
                crop.CropY + crop.CropHeight > 1)
            {
                throw new BadRequestException("Crop area exceeds image bounds.");
            }
        }

        // VALIDATE: ratio compared to FeedAspectRatio
        private static void ValidateAspectRatioCompatibility(
            PostMediaCropInfoRequest crop,
            AspectRatioEnum feedAspectRatio
        )
        {
            float expectedRatio = feedAspectRatio switch
            {
                AspectRatioEnum.Square => 1f,          // 1:1
                AspectRatioEnum.Portrait => 4f / 5f,   // 4:5
                AspectRatioEnum.Landscape => 16f / 9f, // 16:9
                _ => 0f
            };

            float actualRatio = crop.CropWidth!.Value / crop.CropHeight!.Value;

            if (Math.Abs(actualRatio - expectedRatio) > RATIO_TOLERANCE)
            {
                //throw new BadRequestException(
                //    $"Crop ratio ({actualRatio:F2}) is not compatible with feed aspect ratio {feedAspectRatio}."
                //);
                Console.WriteLine(
                      $"[Crop Warning] Crop ratio {actualRatio:F2} does not match feed ratio {expectedRatio:F2} ({feedAspectRatio})"
                );
            }
        }

        private static bool IsBetween0And1(float value)
            => value >= 0f && value <= 1f;
    }

}
