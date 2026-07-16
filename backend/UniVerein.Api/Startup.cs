using System;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UniVerein.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using UniVerein.Api.Services;
using UniVerein.Api.Services.Sepa;
using UniVerein.DAL.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using UniVerein.Api.Services.Firmware;

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
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            var connectionString = _configuration.GetConnectionString("Default");

            if (!string.IsNullOrEmpty(connectionString))
            {
                services.AddDbContext<AppDbContext>(opt =>
                {
                    if (connectionString.Contains("Data Source"))
                        opt.UseSqlite(connectionString);
                    else
                        opt.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));
                });
            }

            services.AddScoped<CryptoService>();
            services.AddScoped<SepaService>();
            services.AddScoped<JwtService>();
            services.AddScoped<MailService>();
            services.AddScoped<ContributionService>();
            services.AddScoped<AuditService>();
            services.AddScoped<BackupService>();
            services.AddScoped<ContributionService>();

            services.AddHostedService<ContributionBackgroundService>();
            services.AddHttpClient<FirmwareService>();
            services.AddHostedService<FirmwareCheckBackgroundService>();
            services.AddHttpContextAccessor();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
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
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;

                            if (!string.IsNullOrEmpty(accessToken) &&
                                path.StartsWithSegments("/emailProgress"))
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddAuthorization();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails();

            services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy
                        .SetIsOriginAllowed(origin =>
                        {
                            var uri = new Uri(origin);
                            return uri.Host == "localhost";
                        })
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseExceptionHandler();
            app.Use(async (ctx, next) =>
            {
                Log.Information($"REQUEST: {ctx.Request.Method} {ctx.Request.Path}");
                await next();
            });

            app.UseCors("AllowFrontend");
            app.UseAuthentication();
            app.UseAuthorization();
        }
    }
}