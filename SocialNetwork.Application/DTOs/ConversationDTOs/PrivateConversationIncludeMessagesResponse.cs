using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.DTOs.ConversationDTOs
{
    public class PrivateConversationIncludeMessagesResponse
    {
        public bool IsNew { get; set; }
        public ConversationMetaData? MetaData { get; set; }
        public PagedResponse<MessageBasicModel> Messages { get; set; } = new PagedResponse<MessageBasicModel>();
    }
}
