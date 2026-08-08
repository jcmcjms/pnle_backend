using Pnle.Domain.Auth;

namespace Pnle.Application.Auth;

public interface IUserRepository
{
    Task<User?> FindByGoogleSubjectAsync(
        string googleSubject,
        CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken);
}