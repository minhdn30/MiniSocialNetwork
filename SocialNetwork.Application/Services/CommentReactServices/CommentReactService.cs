using AutoMapper;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Application.DTOs.PostReactDTOs;
using SocialNetwork.Domain.Entities;
using SocialNetwork.Domain.Enums;
using SocialNetwork.Infrastructure.Models;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.CommentReacts;
using SocialNetwork.Infrastructure.Repositories.Comments;
using SocialNetwork.Infrastructure.Repositories.Posts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocialNetwork.Application.Services.CommentReactServices
{
    public class CommentReactService : ICommentReactService
    {
        private readonly ICommentRepository _commentRepository;
        private readonly ICommentReactRepository _commentReactRepository;
        private readonly IPostRepository _postRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        public CommentReactService(ICommentRepository commentRepository, ICommentReactRepository commentReactRepository, IPostRepository postRepository,
            IAccountRepository accountRepository, IMapper mapper)
        {
            _commentRepository = commentRepository;
            _commentReactRepository = commentReactRepository;
            _postRepository = postRepository;
            _accountRepository = accountRepository;
            _mapper = mapper;
        }
        public async Task<ReactToggleResponse> ToggleReactOnComment(Guid commentId, Guid accountId)
        {
            var existingReact = await _commentReactRepository.GetUserReactOnCommentAsync(commentId, accountId);
            var isReactedByCurrentUser = false;
            if (existingReact != null)
            {
                await _commentReactRepository.RemoveCommentReact(existingReact);
                isReactedByCurrentUser = false;
            }
            else
            {
                var newReact = new CommentReact
                {
                    CommentId = commentId,
                    AccountId = accountId,
                    ReactType = ReactEnum.Love,
                    CreatedAt = DateTime.UtcNow
                };
                await _commentReactRepository.AddCommentReact(newReact);
                isReactedByCurrentUser = true;
            }
            var reactCount = await _commentReactRepository.GetReactCountByCommentId(commentId);
            return new ReactToggleResponse
            {
                ReactCount = reactCount,
                IsReactedByCurrentUser = isReactedByCurrentUser
            };
        }
        public async Task<PagedResponse<AccountReactListModel>> GetAccountsReactOnCommentPaged(Guid commentId, Guid? currentId, int page, int pageSize)
        {
            var (reacts, totalItems) = await _commentReactRepository.GetAccountsReactOnCommentPaged(commentId, currentId, page, pageSize);
            return new PagedResponse<AccountReactListModel>
            {
                Items = reacts,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };
        }
    }
}
