using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pnle.Application.Auth;
using Pnle.Domain.Auth;

namespace Pnle.Infrastructure.Auth;

public sealed class JwtAccessTokenIssuer(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider)
    : IAccessTokenIssuer
{
    public AccessToken Issue(User user)
    {
        var jwtOptions = options.Value;

        var expiresAtUtc = timeProvider
            .GetUtcNow()
            .AddMinutes(jwtOptions.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtClaimNames.Subject, user.Id.ToString()),
            new(JwtClaimNames.Email, user.Email)
        };

        if (!string.IsNullOrWhiteSpace(user.Name))
        {
            claims.Add(new Claim(JwtClaimNames.Name, user.Name));
        }

        if (!string.IsNullOrWhiteSpace(user.PictureUrl))
        {
            claims.Add(new Claim(JwtClaimNames.Picture, user.PictureUrl));
        }

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: signingCredentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessToken(tokenValue, expiresAtUtc);
    }
}
