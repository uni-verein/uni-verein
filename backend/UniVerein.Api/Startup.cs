using System;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using UniVerein.Api.Extensions;
using UniVerein.Api.Security;
using Microsoft.EntityFrameworkCore;
using UniVerein.Api.Services;
using UniVerein.Api.Services.Sepa;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using UniVerein.Api.Services.Demo;
using UniVerein.Api.Services.Firmware;
using UniVerein.Api.Services.Calendar;
using Microsoft.AspNetCore.Http;
using IPNetwork = System.Net.IPNetwork;

namespace UniVerein.Api
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<LoginAttemptTracker>();
            services.AddSingleton<AccountLockoutTracker>();
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            string? connectionString = _configuration.GetConnectionString("Default");

            if (!string.IsNullOrEmpty(connectionString))
            {
                services.AddDbContext<AppDbContext>(opt =>
                {
                    if (connectionString.Contains("Data Source"))
                        opt.UseSqlite(connectionString);
                    else
                        opt.UseNpgsql(connectionString);
                });
            }

            services.AddScoped<CryptoService>();
            services.AddScoped<SepaService>();
            services.AddScoped<JwtService>();
            services.AddScoped<TwoFactorService>();
            services.AddScoped<MailService>();
            services.AddScoped<ContributionService>();
            services.AddScoped<AuditService>();
            services.AddScoped<MemberService>();
            services.AddScoped<BackupService>();
            services.AddScoped<ContributionService>();
            services.AddScoped<ReceiptService>();
            services.AddScoped<IncomeService>();
            services.AddScoped<FinanceAnalyticsService>();
            services.AddScoped<ClubIdentityService>();
            services.AddScoped<AcknowledgementService>();
            services.AddScoped<BookingNumberService>();
            services.AddScoped<EurExportService>();
            services.AddSingleton<EurWorkbookWriter>();
            services.AddSingleton<AcknowledgementPdfService>();
            services.AddScoped<ReceiptNotificationService>();
            services.AddScoped<ReferenceDataService>();
            services.AddScoped<PendingSelfEnrollmentService>();
            services.AddScoped<SelfEnrollmentNotificationService>();
            services.AddScoped<CalendarNotificationService>();
            services.AddScoped<CalendarReminderService>();
            services.AddSingleton<CalendarMailQueue>();

            services.AddScoped<DemoDataService>();
            if (DemoMode.IsResetEnabled(_configuration))
                services.AddHostedService<DemoResetBackgroundService>();

            services.AddHostedService<ContributionBackgroundService>();
            services.AddHttpClient<FirmwareService>();
            services.AddHostedService<FirmwareCheckBackgroundService>();
            services.AddHostedService<PendingSelfEnrollmentCleanupService>();
            services.AddHostedService<CalendarMailBackgroundService>();
            services.AddHostedService<CalendarReminderBackgroundService>();
            services.AddHttpContextAccessor();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    byte[] key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidIssuer = _configuration["Jwt:Issuer"],
                        ValidAudience = _configuration["Jwt:Issuer"],
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key)
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            if (!context.Request.Headers.ContainsKey("Authorization") &&
                                AuthCookie.TryRead(context.Request, out string cookieToken) &&
                                (AuthCookie.IsSafeMethod(context.Request) || AuthCookie.HasCsrfHeader(context.Request)))
                            {
                                context.Token = cookieToken;
                            }

                            return Task.CompletedTask;
                        },
                        OnTokenValidated = async context =>
                        {
                            string? userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                            string? roleClaim = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;
                            string? sessionVersionClaim = context.Principal?.FindFirst(JwtService.SessionVersionClaim)?.Value;
                            bool valid = false;
                            if (Guid.TryParse(userIdClaim, out Guid userId) &&
                                Enum.TryParse(roleClaim, out UserRole role) && Enum.IsDefined(role) &&
                                int.TryParse(sessionVersionClaim, out int sessionVersion))
                            {
                                AppDbContext db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                                valid = await db.Users.AnyAsync(u =>
                                    u.Id == userId && u.Role == role && u.SessionVersion == sessionVersion);
                            }

                            if (!valid)
                            {
                                Log.Warning("Startup: Rejected token of unknown, deleted or changed user or revoked session {UserId} on {Method} {Path}.",
                                    userId, context.Request.Method, RequestLogSanitizer.Redact(context.Request.Path));
                                context.Fail("The user of this token does not exist, has another role or the session was revoked.");
                            }
                        },
                        OnAuthenticationFailed = context =>
                        {
                            Log.Warning("Startup: JWT authentication failed on {Method} {Path}, {ExceptionType}.",
                                context.Request.Method, RequestLogSanitizer.Redact(context.Request.Path), context.Exception.GetType().Name);
                            return Task.CompletedTask;
                        },
                        OnForbidden = context =>
                        {
                            Log.Warning("Startup: User {UserId} with role {Role} is not allowed to access {Method} {Path}.",
                                context.HttpContext.GetActorId(), context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? "none",
                                context.Request.Method, RequestLogSanitizer.Redact(context.Request.Path));
                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails();

            string[] corsAllowedOrigins = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                                          ?? Array.Empty<string>();

            services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy
                        .WithOrigins(corsAllowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            static int GetPermitLimit(HttpContext httpContext, string key, int defaultValue)
            {
                return httpContext.RequestServices.GetRequiredService<IConfiguration>().GetValue(key, defaultValue);
            }

            static void AddIpRateLimitPolicy(RateLimiterOptions options, string policyName, string configKey, int defaultPermitLimit)
            {
                options.AddPolicy(policyName, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.GetClientIpAddress(),
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = GetPermitLimit(httpContext, configKey, defaultPermitLimit),
                            Window = TimeSpan.FromMinutes(15),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
            }

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, _) =>
                {
                    Log.Warning("Startup: Rate limit exceeded on {Method} {Path}.",
                        context.HttpContext.Request.Method, RequestLogSanitizer.Redact(context.HttpContext.Request.Path));
                    return ValueTask.CompletedTask;
                };

                AddIpRateLimitPolicy(options, "self-enrollment", "RateLimiting:SelfEnrollment:PermitLimit", 5);
                AddIpRateLimitPolicy(options, "self-enrollment-read", "RateLimiting:SelfEnrollment:ReadPermitLimit", 20);
                AddIpRateLimitPolicy(options, "self-enrollment-confirm", "RateLimiting:SelfEnrollment:ConfirmPermitLimit", 10);
                AddIpRateLimitPolicy(options, "auth-login", "RateLimiting:AuthLogin:PermitLimit", 20);
                // Calendar apps poll feeds regularly and many members may share one IP (e.g. a university network).
                AddIpRateLimitPolicy(options, "calendar-feed", "RateLimiting:CalendarFeed:PermitLimit", 300);
                AddIpRateLimitPolicy(options, "public-calendar", "RateLimiting:PublicCalendar:PermitLimit", 300);
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor,
                ForwardedForHeaderName = "X-Real-IP",
                KnownIPNetworks =
                {
                    IPNetwork.Parse("172.16.0.0/12"),
                    IPNetwork.Parse("192.168.0.0/16")
                }
            });

            app.UseSerilogRequestLogging(options =>
            {
                options.GetLevel = RequestLogLevel.GetLevel;
                options.GetMessageTemplateProperties = RequestLogSanitizer.GetMessageTemplateProperties;
            });
            app.UseExceptionHandler();

            app.UseCors("AllowFrontend");
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
        }
    }
}