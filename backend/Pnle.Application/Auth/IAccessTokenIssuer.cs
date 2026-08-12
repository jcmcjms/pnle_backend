using Pnle.Domain.Auth;

namespace Pnle.Application.Auth;

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}
