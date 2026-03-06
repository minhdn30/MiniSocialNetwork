using CloudM.Application.DTOs.CommonDTOs;
using CloudM.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudM.Application.DTOs.ConversationDTOs
{
    public class PrivateConversationIncludeMessagesResponse
    {
        public bool IsNew { get; set; }
        public ConversationMetaData? MetaData { get; set; }
        public CursorResponse<MessageBasicModel> Messages { get; set; } = new CursorResponse<MessageBasicModel>();
    }
}
