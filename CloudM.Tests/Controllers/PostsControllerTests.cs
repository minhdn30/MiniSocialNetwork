using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using CloudM.API.Controllers;
using CloudM.Application.DTOs.PostDTOs;
using CloudM.Application.Services.CommentServices;
using CloudM.Application.Services.PostReactServices;
using CloudM.Application.Services.PostSaveServices;
using CloudM.Application.Services.PostServices;
using CloudM.Application.Services.PostTagServices;
using CloudM.Infrastructure.Models;

namespace CloudM.Tests.Controllers
{
    public class PostsControllerTests
    {
        private readonly Mock<IPostService> _mockPostService = new();
        private readonly Mock<IPostReactService> _mockPostReactService = new();
        private readonly Mock<IPostSaveService> _mockPostSaveService = new();
        private readonly Mock<IPostTagService> _mockPostTagService = new();
        private readonly Mock<ICommentService> _mockCommentService = new();

        [Fact]
        public async Task GetFeedPostsByScore_WithoutTokenMode_UsesLegacyFeedFlow()
        {
            // arrange
            var currentId = Guid.NewGuid();
            var feed = new List<PostFeedModel>
            {
                new()
                {
                    PostId = Guid.NewGuid(),
                    PostCode = "LEGACY001",
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockPostService
                .Setup(x => x.GetFeedByScoreAsync(currentId, null, null, 5))
                .ReturnsAsync(feed);

            var controller = CreateController(currentId);

            // act
            var result = await controller.GetFeedPostsByScore(5, null, null, null, null);

            // assert
            result.Should().BeOfType<OkObjectResult>();
            _mockPostService.Verify(x => x.GetFeedByScoreAsync(currentId, null, null, 5), Times.Once);
            _mockPostService.Verify(x => x.GetFeedPageAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetFeedPostsByScore_WithTokenMode_UsesTokenFeedFlow()
        {
            // arrange
            var currentId = Guid.NewGuid();
            var response = new PostFeedCursorResponse
            {
                Items = new List<PostFeedModel>
                {
                    new()
                    {
                        PostId = Guid.NewGuid(),
                        PostCode = "TOKEN001",
                        CreatedAt = DateTime.UtcNow
                    }
                }
            };

            _mockPostService
                .Setup(x => x.GetFeedPageAsync(currentId, null, 6))
                .ReturnsAsync(response);

            var controller = CreateController(currentId);

            // act
            var result = await controller.GetFeedPostsByScore(6, "token", null, null, null);

            // assert
            result.Should().BeOfType<OkObjectResult>();
            _mockPostService.Verify(x => x.GetFeedPageAsync(currentId, null, 6), Times.Once);
            _mockPostService.Verify(x => x.GetFeedByScoreAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetFeedPostsByScore_WithCursorToken_UsesTokenFeedFlow()
        {
            // arrange
            var currentId = Guid.NewGuid();
            const string cursorToken = "cursor-token";

            _mockPostService
                .Setup(x => x.GetFeedPageAsync(currentId, cursorToken, 7))
                .ReturnsAsync(new PostFeedCursorResponse());

            var controller = CreateController(currentId);

            // act
            var result = await controller.GetFeedPostsByScore(7, null, cursorToken, null, null);

            // assert
            result.Should().BeOfType<OkObjectResult>();
            _mockPostService.Verify(x => x.GetFeedPageAsync(currentId, cursorToken, 7), Times.Once);
            _mockPostService.Verify(x => x.GetFeedByScoreAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetFeedPostsByScore_WithMixedTokenModeAndLegacyCursor_ReturnsBadRequest()
        {
            // arrange
            var controller = CreateController(Guid.NewGuid());

            // act
            var result = await controller.GetFeedPostsByScore(
                5,
                "token",
                null,
                DateTime.UtcNow,
                Guid.NewGuid());

            // assert
            result.Should().BeOfType<BadRequestObjectResult>();
            _mockPostService.Verify(x => x.GetFeedPageAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
            _mockPostService.Verify(x => x.GetFeedByScoreAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetFeedPostsByScore_WithPartialLegacyCursor_ReturnsBadRequest()
        {
            // arrange
            var controller = CreateController(Guid.NewGuid());

            // act
            var result = await controller.GetFeedPostsByScore(
                5,
                null,
                null,
                DateTime.UtcNow,
                null);

            // assert
            result.Should().BeOfType<BadRequestObjectResult>();
            _mockPostService.Verify(x => x.GetFeedPageAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
            _mockPostService.Verify(x => x.GetFeedByScoreAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>()), Times.Never);
        }

        private PostsController CreateController(Guid currentId)
        {
            var controller = new PostsController(
                _mockPostService.Object,
                _mockPostReactService.Object,
                _mockPostSaveService.Object,
                _mockPostTagService.Object,
                _mockCommentService.Object);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[]
                            {
                                new Claim(ClaimTypes.NameIdentifier, currentId.ToString())
                            },
                            authenticationType: "TestAuth"))
                }
            };

            return controller;
        }
    }
}
