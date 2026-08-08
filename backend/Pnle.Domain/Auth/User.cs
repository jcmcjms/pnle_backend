namespace Pnle.Domain.Auth;

public sealed class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string GoogleSubject { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? Name { get; private set; }

    public string? PictureUrl { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastLoginAtUtc { get; private set; }

    private User()
    {
        // Required by EF Core.
    }

    public static User CreateFromGoogle(
        GoogleUserProfile profile,
        DateTimeOffset now)
    {
        return new User
        {
            GoogleSubject = profile.Subject,
            Email = profile.Email,
            Name = profile.Name,
            PictureUrl = profile.PictureUrl,
            CreatedAtUtc = now,
            LastLoginAtUtc = now
        };
    }

    public void UpdateFromGoogle(
        GoogleUserProfile profile,
        DateTimeOffset now)
    {
        Email = profile.Email;
        Name = profile.Name;
        PictureUrl = profile.PictureUrl;
        LastLoginAtUtc = now;
    }
}