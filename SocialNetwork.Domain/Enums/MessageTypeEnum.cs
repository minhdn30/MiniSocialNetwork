using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Enums
{
    public enum MessageTypeEnum
    {
        Text = 1,    // Tin nhắn chữ bình thường
        Media = 2,   // Tin nhắn có ảnh / video / file
        System = 3   // Tin nhắn hệ thống (add member, leave, rename group...)
    }

}
