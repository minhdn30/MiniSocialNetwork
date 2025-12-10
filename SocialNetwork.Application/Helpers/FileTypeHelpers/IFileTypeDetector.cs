using Microsoft.AspNetCore.Http;
using SocialNetwork.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Helpers.FileTypeHelpers
{
    public interface IFileTypeDetector
    {
        Task<MediaTypeEnum?> GetMediaTypeAsync(IFormFile file);
    }
}
