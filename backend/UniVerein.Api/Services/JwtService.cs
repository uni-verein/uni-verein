using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using UniVerein.Api.Security;
using UniVerein.DAL.Entities;
using Microsoft.Extensions.Configuration;

namespace UniVerein.Api.Services;

public class JwtService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);
    public const string SessionVersionClaim = "session_version";

    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string CreateToken(UserEntity userEntity)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Issuer"],
            claims: new List<Claim>()
            {
                new Claim(ClaimTypes.NameIdentifier, userEntity.Id.ToString()),
                new Claim(ClaimTypes.Name, userEntity.Username),
                new Claim(ClaimTypes.Role, userEntity.Role.ToString()),
                new Claim(SessionVersionClaim, userEntity.SessionVersion.ToString(), ClaimValueTypes.Integer32)
            },
            expires: DateTime.UtcNow.Add(Lifetime),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string IssueSession(HttpContext context, UserEntity userEntity)
    {
        string token = CreateToken(userEntity);
        AuthCookie.Append(context, token, DateTimeOffset.UtcNow.Add(Lifetime));
        return token;
    }
}