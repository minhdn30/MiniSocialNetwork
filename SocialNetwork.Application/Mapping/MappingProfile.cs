using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.PinnedMessageDTOs;
using SocialNetwork.Application.DTOs.AccountSettingDTOs;
using SocialNetwork.Application.DTOs.AuthDTOs;
using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.DTOs.ConversationDTOs;
using SocialNetwork.Application.DTOs.ConversationMemberDTOs;
using SocialNetwork.Application.DTOs.MessageDTOs;
using SocialNetwork.Application.DTOs.MessageMediaDTOs;
using SocialNetwork.Application.DTOs.PostDTOs;
using SocialNetwork.Application.DTOs.PostMediaDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Application.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            //Account mappings
            CreateMap<Account, RegisterResponse>();
            CreateMap<RegisterDTO, Account>()
                .ForMember(dest => dest.AccountId, opt => opt.MapFrom(_ => Guid.NewGuid()))
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.Username.ToLower()));
            CreateMap<Account, AccountOverviewResponse>()
                .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.RoleName));
            CreateMap<Account, AccountDetailResponse>()
                .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.RoleName))
                .ForMember(dest => dest.Settings, opt => opt.MapFrom(src => src.Settings));
            CreateMap<AccountSettings, AccountSettingsResponse>();
            CreateMap<AccountCreateRequest, Account>()
                .ForMember(dest => dest.AccountId, opt => opt.MapFrom(_ => Guid.NewGuid()))
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore());
            CreateMap<AccountUpdateRequest, Account>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
            CreateMap<ProfileUpdateRequest, Account>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
            CreateMap<AccountSettingsUpdateRequest, AccountSettings>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
             CreateMap<Account, ProfileDetailResponse>();
            //Account -> Post mappings
            CreateMap<Account, AccountBasicInfoResponse>();
            CreateMap<Account, AccountChatInfoResponse>()
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.Status == AccountStatusEnum.Active));
            //Post mappings
            CreateMap<PostCreateRequest, Post>()
                .ForMember(dest => dest.PostId, opt => opt.MapFrom(_ => Guid.NewGuid()))
                .ForMember(dest => dest.AccountId, opt => opt.Ignore())
                .ForMember(dest => dest.Medias, opt => opt.Ignore());
            CreateMap<PostUpdateRequest, Post>()
                .ForMember(dest => dest.Medias, opt => opt.Ignore())
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
            CreateMap<Post, PostDetailResponse>()
                .ForMember(dest => dest.Owner, opt => opt.MapFrom(src => src.Account))
                .ForMember(dest => dest.Medias, opt => opt.MapFrom(src => src.Medias))
                .ForMember(dest => dest.TotalComments, opt => opt.MapFrom(src => src.Comments.Count(c => c.ParentCommentId == null)))
                .ForMember(dest => dest.TotalReacts, opt => opt.MapFrom(src => src.Reacts.Count))
                .ForMember(dest => dest.TotalMedias, opt => opt.MapFrom(src => src.Medias.Count));

            //Post Media mappings
            CreateMap<PostMediaCreateRequest, PostMedia>();
            CreateMap<PostMedia, PostMediaDetailResponse>();

            //Comment mappings
            CreateMap<CommentCreateRequest, Comment>()
                .ForMember(dest => dest.CommentId, opt => opt.MapFrom(_ => Guid.NewGuid()))
                .ForMember(dest => dest.AccountId, opt => opt.Ignore())
                .ForMember(dest => dest.PostId, opt => opt.Ignore());
            CreateMap<CommentUpdateRequest, Comment>()
                .ForMember(dest => dest.CommentId, opt => opt.Ignore())
                .ForMember(dest => dest.AccountId, opt => opt.Ignore())
                .ForMember(dest => dest.PostId, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));
            CreateMap<Comment, CommentResponse>()
                .ForMember(dest => dest.Owner, opt => opt.MapFrom(src => src.Account))
                .ForMember(dest => dest.Owner, opt => opt.Ignore());

            //Conversation mappings
            CreateMap<Conversation, ConversationResponse>();

            //Conversation Member mappings
            CreateMap<ConversationMember, ConversationMemberResponse>();

            //Message mappings
            CreateMap<SendMessageInPrivateChatRequest, Message>()
                .ForMember(dest => dest.MessageId, opt => opt.MapFrom(_ => Guid.NewGuid()))
                .ForMember(dest => dest.ConversationId, opt => opt.Ignore())
                .ForMember(dest => dest.AccountId, opt => opt.Ignore())
                .ForMember(dest => dest.MessageType, opt => opt.Ignore());
            CreateMap<Message, SendMessageResponse>()
                .ForMember(dest => dest.Sender, opt => opt.MapFrom(src => src.Account))
                .ForMember(dest => dest.Medias, opt => opt.MapFrom(src => src.Medias));

            //Message Media mappings
            CreateMap<MessageMedia, MessageMediaResponse>();

            // Infrastructure model mappings
            CreateMap<AccountBasicInfoModel, AccountBasicInfoResponse>();
            CreateMap<AccountChatInfoModel, AccountChatInfoResponse>();
            CreateMap<MessageMediaBasicModel, MessageMediaResponse>();
            CreateMap<PinnedMessageModel, PinnedMessageResponse>();
            CreateMap<CommentWithReplyCountModel, CommentResponse>();
            CreateMap<ReplyCommentModel, CommentResponse>();
        }
    }
}
