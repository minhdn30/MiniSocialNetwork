using Microsoft.AspNetCore.Http;
using CloudM.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.AccountDTOs
{
    public class ProfileUpdateRequest
    {
        [StringLength(100, ErrorMessage = "Fullname cannot be longer than 100 characters.")]
        public string? FullName { get; set; }
        public IFormFile? AvatarFile { get; set; }
        public bool? DeleteAvatar { get; set; }
        public IFormFile? CoverFile { get; set; }
        public bool? DeleteCover { get; set; }
        [StringLength(15, ErrorMessage = "Phone cannot be longer than 15 characters.")]
        [Phone(ErrorMessage = "Phone is invalid.")]
        public string? Phone { get; set; }
        public bool? Gender { get; set; }
        [StringLength(500, ErrorMessage = "Bio cannot be longer than 500 characters.")]
        public string? Bio { get; set; }
        [StringLength(255, ErrorMessage = "Address cannot be longer than 255 characters.")]
        public string? Address { get; set; }
        public AccountPrivacyEnum? PhonePrivacy { get; set; }
        public AccountPrivacyEnum? AddressPrivacy { get; set; }
    }
}
