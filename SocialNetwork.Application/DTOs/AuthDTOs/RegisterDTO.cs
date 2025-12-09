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
        [StringLength(100, ErrorMessage = "Username cannot be longer than 100 characters.")]

        public string Username { get; set; } = null!;
        [Required(ErrorMessage = "Email is required.")]
        [StringLength(100, ErrorMessage = "Email cannot be longer than 100 characters.")]

        public string Email { get; set; } = null!;
        [Required(ErrorMessage = "Fullname is required.")]
        [StringLength(100, ErrorMessage = "Fullname cannot be longer than 100 characters.")]

        public string FullName { get; set; } = null!;
        [Required(ErrorMessage = "Password is required.")]

        public string Password { get; set; } = null!;
    }
}
