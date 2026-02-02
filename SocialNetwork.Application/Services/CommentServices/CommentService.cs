using AutoMapper;
using SocialNetwork.Application.DTOs.AccountDTOs;
using SocialNetwork.Application.DTOs.CommentDTOs;
using SocialNetwork.Application.DTOs.CommonDTOs;
using SocialNetwork.Domain.Entities;
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
using static SocialNetwork.Application.Exceptions.CustomExceptions;

namespace SocialNetwork.Application.Services.CommentServices
{
    public class CommentService : ICommentService
    {
        private readonly ICommentRepository _commentRepository;
        private readonly ICommentReactRepository _commentReactRepository;
        private readonly IPostRepository _postRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;
        public CommentService(ICommentRepository commentRepository, ICommentReactRepository commentReactRepository, IPostRepository postRepository,
            IAccountRepository accountRepository, IMapper mapper)
        {
            _commentRepository = commentRepository;
            _commentReactRepository = commentReactRepository;
            _postRepository = postRepository;
            _accountRepository = accountRepository;
            _mapper = mapper;
        }
        public async Task<CommentResponse> AddCommentAsync(Guid postId, Guid accountId, CommentCreateRequest request)
        {
            if(!await _postRepository.IsPostExist(postId))
            {
                throw new BadRequestException($"Post with ID {postId} not found.");
            }
 
            if(!await _accountRepository.IsAccountIdExist(accountId))
            {
                throw new BadRequestException($"Account with ID {accountId} not found.");
            }
            if (request.ParentCommentId.HasValue)
            {
                var parentId = request.ParentCommentId.Value;

                if (!await _commentRepository.IsCommentExist(parentId) ||
                    !await _commentRepository.IsCommentCanReply(parentId))
                {
                    var message = !await _commentRepository.IsCommentExist(parentId)
                        ? $"Parent comment with ID {parentId} not found."
                        : "Cannot reply to a reply. Only one level of reply is allowed.";

                    throw new BadRequestException(message);
                }
            }

            var comment = _mapper.Map<Comment>(request);
            comment.PostId = postId;
            comment.AccountId = accountId;
            await _commentRepository.AddComment(comment);
            
            var result = _mapper.Map<CommentResponse>(comment);
            
            // Populate Owner info for realtime rendering
            var account = await _accountRepository.GetAccountById(accountId);
            if (account != null)
            {
                result.Owner = _mapper.Map<AccountBasicInfoResponse>(account);
            }

            result.TotalCommentCount = await _commentRepository.CountCommentsByPostId(postId);
            return result;
        }
        public async Task<CommentResponse> UpdateCommentAsync(Guid commentId, Guid accountId, CommentUpdateRequest request)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            if (comment == null)
            {
                throw new BadRequestException($"Comment with ID {commentId} not found.");
            }
            if(!await _postRepository.IsPostExist(comment.PostId))
            {
                throw new BadRequestException($"Post with ID {comment.PostId} not found.");
            }
            if (comment.AccountId != accountId)
            {
                throw new ForbiddenException("You are not authorized to update this comment.");
            }
            _mapper.Map(request, comment);
            await _commentRepository.UpdateComment(comment);
            var result = _mapper.Map<CommentResponse>(comment);
            result.ReactCount = await _commentReactRepository.CountCommentReactAsync(comment.CommentId);
            result.ReplyCount = await _commentRepository.CountCommentRepliesAsync(comment.CommentId);
            return result;
        }
        public async Task<Guid?> DeleteCommentAsync(Guid commentId,  Guid accountId, bool isAdmin)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            if (comment == null)
            {
                throw new BadRequestException($"Comment with ID {commentId} not found.");
            }
            if (!await _postRepository.IsPostExist(comment.PostId))
            {
                throw new BadRequestException($"Post with ID {comment.PostId} not found.");
            }
            if (comment.AccountId != accountId && !isAdmin)
            {
                throw new ForbiddenException("You are not authorized to delete this comment.");
            }
            await _commentRepository.DeleteCommentWithReplies(commentId);
            return comment.PostId;
        }
        public async Task<PagedResponse<CommentWithReplyCountModel>> GetCommentsByPostIdAsync(Guid postId, Guid? currentId, int page, int pageSize)
        {
            if(!await _postRepository.IsPostExist(postId))
                throw new BadRequestException($"Post with ID {postId} not found.");
            var (items, totalItems) = await _commentRepository.GetCommentsByPostIdWithReplyCountAsync(postId, currentId, page, pageSize);

            return new PagedResponse<CommentWithReplyCountModel>
            {
                Items = items,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            };
        }
        public async Task<PagedResponse<ReplyCommentModel>> GetRepliesByCommentIdAsync(Guid commentId, Guid? currentId, int page, int pageSize)
        {
            if (!await _commentRepository.IsCommentExist(commentId))
                throw new BadRequestException($"Comment with ID {commentId} not found.");
            var (items, totalItems) = await _commentRepository.GetRepliesByCommentIdAsync(commentId, currentId, page, pageSize);

            return new PagedResponse<ReplyCommentModel>
            {
                Items = items,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize
            };
        }
        public async Task<CommentResponse?> GetCommentByIdAsync(Guid commentId)
        {
            var comment = await _commentRepository.GetCommentById(commentId);
            return comment != null ? _mapper.Map<CommentResponse>(comment) : null;
        }

        public async Task<int> GetReplyCountAsync(Guid commentId)
        {
            return await _commentRepository.CountCommentRepliesAsync(commentId);
        }
    }
}
