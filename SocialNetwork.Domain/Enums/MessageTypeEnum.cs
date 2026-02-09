using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Domain.Enums
{
    public enum MessageTypeEnum
    {
        Text = 1,    // msg text
        Media = 2,   // msg image / video / file
        System = 3   // system msg (add member, leave, rename group...)
    }
}
