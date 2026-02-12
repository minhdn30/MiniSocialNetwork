
using Google.Apis.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using SocialNetwork.API.Hubs;
using SocialNetwork.API.Middleware;
using SocialNetwork.Application.Helpers.FileTypeHelpers;
using SocialNetwork.Application.Helpers.SwaggerHelpers;
using SocialNetwork.Application.Mapping;
using SocialNetwork.Application.Services.AccountServices;
using SocialNetwork.Application.Services.AccountSettingServices;
using SocialNetwork.Application.Services.AuthServices;
using SocialNetwork.Infrastructure.Services.Cloudinary;
using SocialNetwork.Application.Services.CommentReactServices;
using SocialNetwork.Application.Services.CommentServices;
using SocialNetwork.Application.Services.ConversationMemberServices;
using SocialNetwork.Application.Services.ConversationServices;
using SocialNetwork.Infrastructure.Services.Email;
using SocialNetwork.Application.Services.EmailVerificationServices;
using SocialNetwork.Application.Services.FollowServices;
using SocialNetwork.Application.Services.JwtServices;
using SocialNetwork.Application.Services.MessageMediaServices;
using SocialNetwork.Application.Services.MessageHiddenServices;
using SocialNetwork.Application.Services.MessageServices;
using SocialNetwork.Application.Services.PostReactServices;
using SocialNetwork.Application.Services.PostServices;
using SocialNetwork.Application.Services.RealtimeServices;
using SocialNetwork.API.Services;
using SocialNetwork.Infrastructure.Data;
using SocialNetwork.Domain.Exceptions;
using SocialNetwork.Infrastructure.Repositories.Accounts;
using SocialNetwork.Infrastructure.Repositories.AccountSettingRepos;
using SocialNetwork.Infrastructure.Repositories.CommentReacts;
using SocialNetwork.Infrastructure.Repositories.Comments;
using SocialNetwork.Infrastructure.Repositories.ConversationMembers;
using SocialNetwork.Infrastructure.Repositories.Conversations;
using SocialNetwork.Infrastructure.Repositories.EmailVerifications;
using SocialNetwork.Infrastructure.Repositories.Follows;
using SocialNetwork.Infrastructure.Repositories.MessageMedias;
using SocialNetwork.Infrastructure.Repositories.Messages;
using SocialNetwork.Infrastructure.Repositories.MessageHiddens;
using SocialNetwork.Infrastructure.Repositories.MessageReacts;
using SocialNetwork.Infrastructure.Repositories.PostMedias;
using SocialNetwork.Infrastructure.Repositories.PostReacts;
using SocialNetwork.Infrastructure.Repositories.Posts;
using SocialNetwork.Infrastructure.Repositories.UnitOfWork;
using System;
using System.Linq;
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
            builder.Services.AddScoped<IFollowRepository, FollowRepository>();
            builder.Services.AddScoped<ICommentRepository, CommentRepository>();
            builder.Services.AddScoped<IPostRepository, PostRepository>();
            builder.Services.AddScoped<IPostMediaRepository, PostMediaRepository>();
            builder.Services.AddScoped<IPostReactRepository, PostReactRepository>();
            builder.Services.AddScoped<ICommentReactRepository, CommentReactRepository>();
            builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
            builder.Services.AddScoped<IConversationMemberRepository, ConversationMemberRepository>();
            builder.Services.AddScoped<IMessageRepository, MessageRepository>();
            builder.Services.AddScoped<IMessageMediaRepository, MessageMediaRepository>();
            builder.Services.AddScoped<IMessageHiddenRepository, MessageHiddenRepository>();
            builder.Services.AddScoped<IMessageReactRepository, MessageReactRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();




            // Services
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IAccountSettingService, AccountSettingService>();
            builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

            builder.Services.AddTransient<IEmailService, EmailService>();
            builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
            builder.Services.AddScoped<IJwtService, JwtService>();
            builder.Services.AddScoped<IFollowService, FollowService>();
            builder.Services.AddScoped<IPostService, PostService>();
            builder.Services.AddScoped<ICommentService, CommentService>();
            builder.Services.AddScoped<IPostReactService, PostReactService>();
            builder.Services.AddScoped<ICommentReactService, CommentReactService>();

            builder.Services.AddScoped<IConversationService, ConversationService>();
            builder.Services.AddScoped<IConversationMemberService, ConversationMemberService>();
            builder.Services.AddScoped<IMessageService, MessageService>();
            builder.Services.AddScoped<IMessageHiddenService, MessageHiddenService>();
            builder.Services.AddScoped<IMessageMediaService, MessageMediaService>();

            // Realtime Services
            builder.Services.AddScoped<IRealtimeService, RealtimeService>();

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
                ? "/swagger/v1/swagger.json"   // relative URL để local dev luôn đúng
                : "/swagger/v1/swagger.json";  // prod
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint(swaggerUrl, "MiniSocialNetwork API V1");
                c.RoutePrefix = "swagger";      // hoặc string.Empty
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
