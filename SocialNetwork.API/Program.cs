
using Google.Apis.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SocialNetwork.API.Hubs;
using SocialNetwork.API.Middleware;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Application.Helpers.SwaggerHelpers;
using SocialNetwork.Application.Mapping;
using SocialNetwork.Application.Services.AccountServices;
using SocialNetwork.Application.Services.AuthServices;
using SocialNetwork.Application.Services.CloudinaryServices;
using SocialNetwork.Application.Services.CommentServices;
using SocialNetwork.Application.Services.EmailServices;
using SocialNetwork.Application.Services.EmailVerificationServices;
using SocialNetwork.Application.Services.FollowServices;
using SocialNetwork.Application.Services.JwtServices;
using SocialNetwork.Application.Services.PostReactServices;
using SocialNetwork.Application.Services.PostServices;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.CommentReacts;
using SocialNetwork.Infrastructure.Repositories.Comments;
using SocialNetwork.Infrastructure.Repositories.EmailVerifications;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.PostMedias;
using SocialNetwork.Infrastructure.Repositories.PostReacts;
using SocialNetwork.Infrastructure.Repositories.Posts;
using System;
using System.Text;
using System.Text.Json;


namespace SocialNetwork.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddUserSecrets<Program>();


            //var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            //                       ?? builder.Configuration.GetConnectionString("Default");

            //override connection string for local development
            var connectionString = "Host=localhost;Port=5432;Database=cloudm;Username=postgres;Password=12345678";

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString)
            );


            // Repositories
            builder.Services.AddScoped<IAccountRepository, AccountRepository>();
            builder.Services.AddScoped<IEmailVerificationRepository, EmailVerificationRepository>();
            builder.Services.AddScoped<IFollowRepository, FollowRepository>();
            builder.Services.AddScoped<ICommentRepository, CommentRepository>();
            builder.Services.AddScoped<IPostRepository, PostRepository>();
            builder.Services.AddScoped<IPostMediaRepository, PostMediaRepository>();
            builder.Services.AddScoped<IPostReactRepository, PostReactRepository>();
            builder.Services.AddScoped<ICommentReactRepository, CommentReactRepository>();


            // Services
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

            builder.Services.AddTransient<IEmailService, EmailService>();
            builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
            builder.Services.AddScoped<IJwtService, JwtService>();
            builder.Services.AddScoped<IFollowService, FollowService>();
            builder.Services.AddScoped<IPostService, PostService>();
            builder.Services.AddScoped<ICommentService, CommentService>();
            builder.Services.AddScoped<IPostReactService, PostReactService>();

            // Helpers
            builder.Services.AddScoped<IFileTypeDetector, FileTypeDetector>();

            // JWT
            var jwtSettings = builder.Configuration.GetSection("Jwt");
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
                };
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                        {
                            context.Response.StatusCode = 401;
                            context.Response.ContentType = "application/json";
                            var result = JsonSerializer.Serialize(new { message = "Token expired" });
                            return context.Response.WriteAsync(result);
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            // AutoMapper
            builder.Services.AddAutoMapper(typeof(MappingProfile));
            //Cors
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .WithOrigins("http://127.0.0.1:5500")   // FE của bạn
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();                   // Quan trọng cho SignalR
                });
            });

            builder.Services.AddControllers();
            builder.Services.AddSignalR();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "CloudM API", Version = "v1" });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Enter token in format: Bearer {token}",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                          Type = ReferenceType.SecurityScheme,
                          Id = "Bearer"
                        },
                        Name = "Bearer",
                        In = ParameterLocation.Header,
                        Scheme = "Bearer"
                    },
                    new List<string>()
                    }
                });
                c.OperationFilter<FileUploadOperation>();
            });

            var app = builder.Build();
            app.UseMiddleware<ExceptionMiddleware>();

            var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
            app.Urls.Add($"http://*:{port}");
            app.UseSwagger();
            var swaggerUrl = builder.Environment.IsDevelopment()
                ? "/swagger/v1/swagger.json"   // relative URL để local dev luôn đúng
                : "/swagger/v1/swagger.json";  // prod
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint(swaggerUrl, "MiniSocialNetwork API V1");
                c.RoutePrefix = "swagger";      // hoặc string.Empty
            });


            // app.UseHttpsRedirection();
            app.UseRouting();
            app.UseCors("AllowAll");

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapHub<PostHub>("/postHub");

            app.MapControllers();

            app.Run();

        }
    }
}
