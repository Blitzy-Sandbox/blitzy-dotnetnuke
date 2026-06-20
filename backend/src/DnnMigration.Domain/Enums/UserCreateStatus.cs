namespace DnnMigration.Domain.Enums;

/// <summary>
/// Represents the outcome of a user-creation / provisioning operation.
/// Consumed by the Application layer user-registration and authentication flows.
/// </summary>
/// <remarks>
/// Ported 1:1 from the legacy DotNetNuke 4.9.0.85 VB.NET enum
/// <c>DotNetNuke.Security.Membership.UserCreateStatus</c>
/// (Library/Components/Users/Membership/UserCreateStatus.vb). The explicit integer
/// values 0-17 are preserved verbatim per the Minimal Change Clause so that any
/// previously persisted or compared values remain valid after the migration to .NET 8.
/// </remarks>
public enum UserCreateStatus
{
    AddUser = 0,
    UsernameAlreadyExists = 1,
    UserAlreadyRegistered = 2,
    DuplicateEmail = 3,
    DuplicateProviderUserKey = 4,
    DuplicateUserName = 5,
    InvalidAnswer = 6,
    InvalidEmail = 7,
    InvalidPassword = 8,
    InvalidProviderUserKey = 9,
    InvalidQuestion = 10,
    InvalidUserName = 11,
    ProviderError = 12,
    Success = 13,
    UnexpectedError = 14,
    UserRejected = 15,
    PasswordMismatch = 16,
    AddUserToPortal = 17
}
