using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.PostDTOs
{
    public class PostSaveStateResponse
    {
        public Guid PostId { get; set; }
        public bool IsSavedByCurrentUser { get; set; }
    }
}
