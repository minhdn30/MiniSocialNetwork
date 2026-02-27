
using Google.Apis.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using CloudM.API.Hubs;
using CloudM.API.Middleware;
using CloudM.Application.Helpers.FileTypeHelpers;
using CloudM.Application.Helpers.StoryHelpers;
using CloudM.Application.Helpers.SwaggerHelpers;
using CloudM.Application.Mapping;
using CloudM.Application.Services.AccountServices;
using CloudM.Application.Services.AccountSettingServices;
using CloudM.Application.Services.AuthServices;
using CloudM.Infrastructure.Services.Cloudinary;
using CloudM.Application.Services.CommentReactServices;
using CloudM.Application.Services.CommentServices;
using CloudM.Application.Services.ConversationMemberServices;
using CloudM.Application.Services.ConversationServices;
using CloudM.Infrastructure.Services.Email;
using CloudM.Application.Services.EmailVerificationServices;
using CloudM.Application.Services.FollowServices;
using CloudM.Application.Services.JwtServices;
using CloudM.Application.Services.MessageMediaServices;
using CloudM.Application.Services.MessageHiddenServices;
using CloudM.Application.Services.MessageReactServices;
using CloudM.Application.Services.MessageServices;
using CloudM.Application.Services.PinnedMessageServices;
using CloudM.Application.Services.PresenceServices;
using CloudM.Application.Services.PostReactServices;
using CloudM.Application.Services.PostServices;
using CloudM.Application.Services.RealtimeServices;
using CloudM.Application.Services.StoryServices;
using CloudM.Application.Services.StoryHighlightServices;
using CloudM.Application.Services.StoryViewServices;
using CloudM.API.Services;
using CloudM.Infrastructure.Data;
using CloudM.Domain.Exceptions;
using CloudM.Infrastructure.Repositories.Accounts;
using CloudM.Infrastructure.Repositories.AccountSettingRepos;
using CloudM.Infrastructure.Repositories.CommentReacts;
using CloudM.Infrastructure.Repositories.Comments;
using CloudM.Infrastructure.Repositories.ConversationMembers;
using CloudM.Infrastructure.Repositories.Conversations;
using CloudM.Infrastructure.Repositories.EmailVerifications;
using CloudM.Infrastructure.Repositories.ExternalLogins;
using CloudM.Infrastructure.Repositories.Follows;
using CloudM.Infrastructure.Repositories.MessageMedias;
using CloudM.Infrastructure.Repositories.Messages;
using CloudM.Infrastructure.Repositories.MessageHiddens;
using CloudM.Infrastructure.Repositories.MessageReacts;
using CloudM.Infrastructure.Repositories.PinnedMessages;
using CloudM.Infrastructure.Repositories.PostMedias;
using CloudM.Infrastructure.Repositories.PostReacts;
using CloudM.Infrastructure.Repositories.Posts;
using CloudM.Infrastructure.Repositories.Presences;
using CloudM.Infrastructure.Repositories.Stories;
using CloudM.Infrastructure.Repositories.StoryHighlights;
using CloudM.Infrastructure.Repositories.StoryViews;
using CloudM.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;


namespace CloudM.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<Program>();

            var connectionString = BuildConnectionString(builder.Configuration, builder.Environment);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'Default' is not configured.");
            }

            try
            {
                var dbInfo = new NpgsqlConnectionStringBuilder(connectionString);
                Console.WriteLine($"[DB] Host={dbInfo.Host};Port={dbInfo.Port};Database={dbInfo.Database};SslMode={dbInfo.SslMode};Pooling={dbInfo.Pooling};Timeout={dbInfo.Timeout};CommandTimeout={dbInfo.CommandTimeout};KeepAlive={dbInfo.KeepAlive}");
            }
            catch
            {
                Console.WriteLine("[DB] Connection string parse warning: could not print normalized DB info.");
            }

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(3), null);
                    npgsqlOptions.CommandTimeout(30);
                })
            );

            // Repositories
            builder.Services.AddScoped<IAccountRepository, AccountRepository>();
            builder.Services.AddScoped<IAccountSettingRepository, AccountSettingRepository>();
            builder.Services.AddScoped<IEmailVerificationRepository, EmailVerificationRepository>();
            builder.Services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>();
            builder.Services.AddScoped<IFollowRepository, FollowRepository>();
            builder.Services.AddScoped<ICommentRepository, CommentRepository>();
            builder.Services.AddScoped<IPostRepository, PostRepository>();
            builder.Services.AddScoped<IPostMediaRepository, PostMediaRepository>();
            builder.Services.AddScoped<IPostReactRepository, PostReactRepository>();
            builder.Services.AddScoped<IStoryRepository, StoryRepository>();
            builder.Services.AddScoped<IStoryHighlightRepository, StoryHighlightRepository>();
            builder.Services.AddScoped<IStoryViewRepository, StoryViewRepository>();
            builder.Services.AddScoped<ICommentReactRepository, CommentReactRepository>();
            builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
            builder.Services.AddScoped<IConversationMemberRepository, ConversationMemberRepository>();
            builder.Services.AddScoped<IMessageRepository, MessageRepository>();
            builder.Services.AddScoped<IMessageMediaRepository, MessageMediaRepository>();
            builder.Services.AddScoped<IMessageHiddenRepository, MessageHiddenRepository>();
            builder.Services.AddScoped<IMessageReactRepository, MessageReactRepository>();
            builder.Services.AddScoped<IPinnedMessageRepository, PinnedMessageRepository>();
            builder.Services.AddScoped<IOnlinePresenceRepository, OnlinePresenceRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Services
            builder.Services.Configure<LoginSecurityOptions>(
                builder.Configuration.GetSection("LoginSecurity"));
            builder.Services.Configure<EmailVerificationSecurityOptions>(
                builder.Configuration.GetSection("EmailVerification"));
            builder.Services.Configure<GoogleAuthOptions>(
                builder.Configuration.GetSection("ExternalAuth:Google"));
            builder.Services.Configure<OnlinePresenceOptions>(
                builder.Configuration.GetSection("OnlinePresence"));
            builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var redisConnectionString =
                    builder.Configuration.GetConnectionString("Redis")
                    ?? builder.Configuration["Redis:ConnectionString"]
                    ?? "localhost:6379,abortConnect=false";

                return ConnectionMultiplexer.Connect(redisConnectionString);
            });

            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IExternalIdentityProvider, GoogleExternalIdentityProvider>();
            builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
            builder.Services.AddScoped<ILoginRateLimitService, RedisLoginRateLimitService>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IAccountSettingService, AccountSettingService>();
            builder.Services.AddSingleton<ICloudinaryDeleteBackgroundQueue, CloudinaryDeleteBackgroundQueue>();
            builder.Services.AddHostedService<CloudinaryDeleteWorkerHostedService>();
            builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

            builder.Services.AddTransient<IEmailService, EmailService>();
            builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
            builder.Services.AddScoped<IEmailVerificationRateLimitService, RedisEmailVerificationRateLimitService>();
            builder.Services.AddScoped<IJwtService, JwtService>();
            builder.Services.AddScoped<IFollowService, FollowService>();
            builder.Services.AddScoped<IPostService, PostService>();
            builder.Services.AddScoped<IStoryService, StoryService>();
            builder.Services.AddScoped<IStoryHighlightService, StoryHighlightService>();
            builder.Services.AddScoped<IStoryViewService, StoryViewService>();
            builder.Services.AddScoped<ICommentService, CommentService>();
            builder.Services.AddScoped<IPostReactService, PostReactService>();
            builder.Services.AddScoped<ICommentReactService, CommentReactService>();

            builder.Services.AddScoped<IConversationService, ConversationService>();
            builder.Services.AddScoped<IConversationMemberService, ConversationMemberService>();
            builder.Services.AddScoped<IMessageService, MessageService>();
            builder.Services.AddScoped<IMessageReactService, MessageReactService>();
            builder.Services.AddScoped<IMessageHiddenService, MessageHiddenService>();
            builder.Services.AddScoped<IMessageMediaService, MessageMediaService>();
            builder.Services.AddScoped<IPinnedMessageService, PinnedMessageService>();

            // Realtime Services
            builder.Services.AddScoped<IRealtimeService, RealtimeService>();
            builder.Services.AddScoped<IOnlinePresenceService, OnlinePresenceService>();
            builder.Services.AddHostedService<EmailVerificationCleanupHostedService>();
            builder.Services.AddHostedService<OnlinePresenceCleanupHostedService>();

            // Helpers
            builder.Services.AddScoped<IStoryRingStateHelper, StoryRingStateHelper>();
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
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings["Key"]!)
                    )
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            (
                                path.StartsWithSegments("/chatHub") ||
                                path.StartsWithSegments("/postHub") ||
                                path.StartsWithSegments("/userHub")
                            ))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },

                    OnAuthenticationFailed = context =>
                    {
                        return Task.CompletedTask;
                    }
                };

            });


            // AutoMapper
            builder.Services.AddAutoMapper(typeof(MappingProfile));
            // Cors
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
            if (allowedOrigins == null || allowedOrigins.Length == 0)
            {
                allowedOrigins = new[]
                {
                    "http://127.0.0.1:5500",
                    "http://localhost:5500",
                    "http://127.0.0.1:5502",
                    "http://localhost:5502",
                    "http://127.0.0.1:5503",
                    "http://localhost:5503",
                    "https://127.0.0.1:5500",
                    "https://localhost:5500",
                    "https://127.0.0.1:5502",
                    "https://localhost:5502",
                    "https://127.0.0.1:5503",
                    "https://localhost:5503"
                };
            }

            var normalizedAllowedOrigins = allowedOrigins
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .Select(origin => origin.Trim().TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var allowLoopbackWildcardInDevelopment = builder.Environment.IsDevelopment();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("FrontendPolicy", policy =>
                {
                    policy
                        .SetIsOriginAllowed(origin =>
                            IsAllowedOrigin(origin, normalizedAllowedOrigins, allowLoopbackWildcardInDevelopment))
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            builder.Services.AddControllers();
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<IUserIdProvider, SignalRAccountIdUserIdProvider>();
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

            // On cloud providers (e.g. Render), bind to PORT over HTTP.
            // Locally, rely on launchSettings/profile URLs so HTTPS profile works.
            var port = Environment.GetEnvironmentVariable("PORT");
            if (!string.IsNullOrWhiteSpace(port))
            {
                app.Urls.Clear();
                app.Urls.Add($"http://*:{port}");
            }
            app.UseSwagger();
            var swaggerUrl = builder.Environment.IsDevelopment()
                ? "/swagger/v1/swagger.json"
                : "/swagger/v1/swagger.json";
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint(swaggerUrl, "CloudM API V1");
                c.RoutePrefix = "swagger";
            });


            // app.UseHttpsRedirection();
            app.UseRouting();
            app.UseCors("FrontendPolicy");

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<AccountStatusMiddleware>();
            app.MapHub<PostHub>("/postHub");
            app.MapHub<UserHub>("/userHub");
            app.MapHub<ChatHub>("/chatHub");

            app.MapControllers();

            app.Run();

        }

        private static string BuildConnectionString(IConfiguration configuration, IHostEnvironment environment)
        {
            var configuredConnectionString = configuration.GetConnectionString("Default");
            var envConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            var rawConnectionString = environment.IsDevelopment()
                ? FirstNonEmpty(configuredConnectionString, envConnectionString, databaseUrl)
                : FirstNonEmpty(databaseUrl, envConnectionString, configuredConnectionString);

            if (string.IsNullOrWhiteSpace(rawConnectionString))
            {
                return string.Empty;
            }

            var sanitized = rawConnectionString.Trim().Trim('"', '\'');

            NpgsqlConnectionStringBuilder csb;
            if (sanitized.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                sanitized.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                csb = BuildFromDatabaseUrl(sanitized);
            }
            else
            {
                csb = new NpgsqlConnectionStringBuilder(sanitized);
            }

            NormalizeHost(csb);
            HardenForTransientNetwork(csb);

            return csb.ToString();
        }

        private static NpgsqlConnectionStringBuilder BuildFromDatabaseUrl(string databaseUrl)
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':', 2);
            var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

            var csb = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = uri.AbsolutePath.Trim('/'),
                Username = username,
                Password = password
            };

            ApplyDatabaseUrlQuery(csb, uri.Query);
            return csb;
        }

        private static void ApplyDatabaseUrlQuery(NpgsqlConnectionStringBuilder csb, string queryString)
        {
            if (string.IsNullOrWhiteSpace(queryString))
            {
                return;
            }

            var query = queryString.TrimStart('?');
            if (string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(keyValue[0]).Trim().ToLowerInvariant();
                var value = keyValue.Length > 1 ? Uri.UnescapeDataString(keyValue[1]).Trim() : string.Empty;

                switch (key)
                {
                    case "sslmode":
                        if (Enum.TryParse<SslMode>(value, true, out var sslMode))
                        {
                            csb.SslMode = sslMode;
                        }
                        break;
                    case "pooling":
                        if (bool.TryParse(value, out var pooling))
                        {
                            csb.Pooling = pooling;
                        }
                        break;
                    case "timeout":
                    case "connect_timeout":
                        if (int.TryParse(value, out var timeout))
                        {
                            csb.Timeout = timeout;
                        }
                        break;
                    case "commandtimeout":
                    case "command_timeout":
                        if (int.TryParse(value, out var commandTimeout))
                        {
                            csb.CommandTimeout = commandTimeout;
                        }
                        break;
                    case "keepalive":
                        if (int.TryParse(value, out var keepAlive))
                        {
                            csb.KeepAlive = keepAlive;
                        }
                        break;
                }
            }
        }

        private static void NormalizeHost(NpgsqlConnectionStringBuilder csb)
        {
            if (string.IsNullOrWhiteSpace(csb.Host))
            {
                return;
            }

            var host = csb.Host.Trim().Trim('"', '\'');
            if (host.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
            {
                host = host["tcp://".Length..];
            }
            else if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            {
                host = host["tcp:".Length..];
            }
            host = host.Trim('/');

            // Handle "hostname:5432" format inside Host.
            var lastColon = host.LastIndexOf(':');
            var singleColon = lastColon > 0 && host.IndexOf(':') == lastColon;
            if (singleColon)
            {
                var portPart = host[(lastColon + 1)..];
                if (int.TryParse(portPart, out var parsedPort))
                {
                    csb.Port = parsedPort;
                    host = host[..lastColon];
                }
            }

            // Handle "hostname,5432" format sometimes seen in copied cloud connection strings.
            var commaIndex = host.LastIndexOf(',');
            if (commaIndex > 0)
            {
                var portPart = host[(commaIndex + 1)..];
                if (int.TryParse(portPart, out var parsedPort))
                {
                    csb.Port = parsedPort;
                    host = host[..commaIndex];
                }
            }

            csb.Host = host.Trim();
        }

        private static void HardenForTransientNetwork(NpgsqlConnectionStringBuilder csb)
        {
            if (csb.Timeout <= 0)
            {
                csb.Timeout = 15;
            }

            if (csb.CommandTimeout <= 0)
            {
                csb.CommandTimeout = 30;
            }

            if (csb.KeepAlive <= 0)
            {
                csb.KeepAlive = 30;
            }

            // Render external Postgres requires TLS. Force secure settings if omitted.
            var host = csb.Host?.ToLowerInvariant() ?? string.Empty;
            if (host.Contains(".render.com") || host.Contains(".render.internal"))
            {
                if (csb.SslMode == SslMode.Disable || csb.SslMode == SslMode.Allow || csb.SslMode == SslMode.Prefer)
                {
                    csb.SslMode = SslMode.Require;
                }
            }
        }

        private static bool IsAllowedOrigin(string? origin, string[] allowedOrigins, bool allowLoopbackWildcardInDevelopment)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                return false;
            }

            var normalizedOrigin = origin.Trim().TrimEnd('/');
            if (allowedOrigins.Any(o => string.Equals(o, normalizedOrigin, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!allowLoopbackWildcardInDevelopment)
            {
                return false;
            }

            if (!Uri.TryCreate(normalizedOrigin, UriKind.Absolute, out var originUri))
            {
                return false;
            }

            var isHttpScheme =
                string.Equals(originUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(originUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            if (!isHttpScheme)
            {
                return false;
            }

            return string.Equals(originUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(originUri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
