namespace DnnMigration.Application.DTOs;

/// <summary>
/// Result of creating a user: the created user projection plus, when the server generated the
/// password on the caller's behalf, the one-time plaintext password.
/// </summary>
/// <remarks>
/// MIGRATION QA finding K: <c>CreateUserDto.RandomPassword</c> instructs the server to generate the
/// password ("Ignored when RandomPassword is true"), but the created <see cref="UserDto"/> deliberately
/// carries NO credential material. Returning the generated plaintext through the read DTO would leak a
/// secret into every subsequent GET projection, so it travels here exactly once — surfaced by the
/// controller in the create response <c>meta</c> (never persisted, never returned on a later read).
/// <para>
/// <see cref="GeneratedPassword"/> is <see langword="null"/> when the caller supplied the password
/// (<c>RandomPassword = false</c>): only a server-generated password is ever returned. This keeps the
/// credential out of the response for the common, caller-supplied-password path.
/// </para>
/// </remarks>
/// <param name="User">The created user projection (never carries credential material).</param>
/// <param name="GeneratedPassword">
/// The one-time plaintext password the server generated when <c>RandomPassword = true</c>; otherwise
/// <see langword="null"/>.
/// </param>
public sealed record CreateUserResult(UserDto User, string? GeneratedPassword);
