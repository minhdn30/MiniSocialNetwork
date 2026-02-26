using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.AuthDTOs
{
    public class RegisterDTO
    {

        [Required(ErrorMessage = "Username is required.")]
        [MinLength(6, ErrorMessage = "Username must be at least 6 characters.")]
        [StringLength(30, ErrorMessage = "Username cannot be longer than 30 characters.")]
        [RegularExpression("^[A-Za-z0-9_]+$", ErrorMessage = "Username can only include letters, numbers, and underscore (_), without spaces or accents.")]

        public string Username { get; set; } = null!;
        [Required(ErrorMessage = "Email is required.")]
        [StringLength(100, ErrorMessage = "Email cannot be longer than 100 characters.")]

        public string Email { get; set; } = null!;
        [Required(ErrorMessage = "Fullname is required.")]
        [MinLength(2, ErrorMessage = "Fullname must be at least 2 characters.")]
        [StringLength(25, ErrorMessage = "Fullname cannot be longer than 25 characters.")]

        public string FullName { get; set; } = null!;
        [Required(ErrorMessage = "Password is required.")]

        public string Password { get; set; } = null!;
    }
}
