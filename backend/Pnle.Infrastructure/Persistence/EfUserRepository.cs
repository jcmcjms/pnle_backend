using Microsoft.EntityFrameworkCore;
using Pnle.Application.Auth;
using Pnle.Domain.Auth;

namespace Pnle.Infrastructure.Persistence;

public sealed class EfUserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByGoogleSubjectAsync(
        string googleSubject,
        CancellationToken cancellationToken)
    {
        return db.Users
            .FirstOrDefaultAsync(x => x.GoogleSubject == googleSubject, cancellationToken);
    }

    public Task<User?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return db.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken)
    {
        await db.Users.AddAsync(user, cancellationToken);
    }
}