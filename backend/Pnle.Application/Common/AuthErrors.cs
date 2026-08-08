namespace Pnle.Application.Common;

public static class AuthErrors
{
    public static readonly Error InvalidGoogleToken = new(
        "AUTH_INVALID_GOOGLE_TOKEN",
        "The Google token is invalid.");

    public static readonly Error EmailNotVerified = new(
        "AUTH_EMAIL_NOT_VERIFIED",
        "The Google email is not verified.");

    public static readonly Error InvalidRefreshToken = new(
        "AUTH_INVALID_REFRESH_TOKEN",
        "The refresh token is invalid or expired.");

    public static readonly Error UserNotFound = new(
        "AUTH_USER_NOT_FOUND",
        "The authenticated user no longer exists.");
}