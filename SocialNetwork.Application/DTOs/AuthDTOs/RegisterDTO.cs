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
        [Required, MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string Username { get; set; } = null!;
        [Required, MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
        public string Email { get; set; } = null!;
        [Required, MaxLength(100)]
        public string FullName { get; set; } = null!;
        [Required]
        public string Password { get; set; } = null!;
    }
}
