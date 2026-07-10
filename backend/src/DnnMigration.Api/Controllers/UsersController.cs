using DnnMigration.Application.DTOs;
using DnnMigration.Application.Interfaces;
using DnnMigration.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DnnMigration.Api.Controllers;

/// <summary>
/// RESTful endpoints for managing users under the <c>/api/users</c> route.
/// </summary>
/// <remarks>
/// <para>
/// MIGRATION: replaces the legacy ASP.NET Web Forms user-administration workflows
/// (<c>Website/admin/Users/**/*.ascx.vb</c> — most notably <c>Users.ascx.vb</c> the user list/grid,
/// <c>User.ascx.vb</c> the single-user add/edit editor, and <c>Password.ascx.vb</c> the
/// change-password screen) together with the data/business surface of
/// <c>Library/Components/Users/UserController.vb</c>. The Web Forms postback/ViewState model is
/// eliminated: <c>Page_Load</c> data fetches become HTTP GETs and the button-click handlers
/// (<c>cmdUpdate_Click</c>, <c>cmdDelete_Click</c>) become PUT/DELETE requests that return JSON only
/// (AAP §0.6.3).
/// </para>
/// <para>
/// This controller follows the canonical thin-CRUD template established by
/// <see cref="PortalsController"/>. It is intentionally THIN: it performs no business logic, no data
/// access, no EF Core, no SQL, and no password hashing (BCrypt hashing lives in the Infrastructure
/// layer behind the Application service). Every operation is delegated to the injected
/// <see cref="IUserService"/>, which owns the application rules; the controller's sole
/// responsibilities are HTTP concerns — model binding, routing, status-code selection, and shaping
/// successful results through the inherited <c>{ "data": ..., "meta": ... }</c> envelope helpers
/// exposed by <see cref="ApiControllerBase"/> (AAP §0.7.1 "no business logic in API controllers").
/// Only DTOs cross this boundary: the EF <c>User</c> entity and the legacy <c>aspnet_Membership</c>
/// credential columns are never exposed. Error responses (including 404 bodies) are produced
/// centrally as RFC 7807 Problem Details by the exception-handling middleware plus
/// <c>AddProblemDetails()</c>; this controller never hand-formats error JSON, and request correlation
/// IDs are attached by <c>CorrelationIdMiddleware</c> in the pipeline (not per action).
/// </para>
/// <para>
/// MIGRATION: legacy <c>UserController.vb</c> exposed <c>Public Shared</c> (static) members that
/// reached the database directly through the provider abstraction; each maps to an injected
/// <see cref="IUserService"/> instance method (AAP §0.6.1 — no statics in application code) and to a
/// REST verb as follows: <c>GetUsers(portalId)</c> (L685) → <c>GET /api/users?portalId={pid}</c>;
/// <c>GetUser</c> (L497) → <c>GET /api/users/{id}</c>; <c>GetUserByName(portalId, username)</c> (L544)
/// → <c>GET /api/users/by-username</c>; <c>CreateUser</c> (L156) → <c>POST /api/users</c>;
/// <c>UpdateUser</c> (L963) → <c>PUT /api/users/{id}</c>; <c>DeleteUser</c> (L200) →
/// <c>DELETE /api/users/{id}</c>; and <c>ChangePassword</c> (L103) →
/// <c>POST /api/users/{id}/change-password</c>.
/// </para>
/// <para>
/// MIGRATION: the class carries <see cref="AuthorizeAttribute"/> so the whole resource is secured by
/// default. This replaces the legacy DotNetNuke <c>PortalSecurity.SecurityAccessLevel</c>
/// (View/Edit/Admin/Host) checks and DES/Forms-authentication gate; access is now proven by a JWT
/// bearer token validated by the ASP.NET Core authentication middleware (AAP §0.6.4).
/// </para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
// MIGRATION (authorization — vertical gate): the caller must be an Administrator or a host
// (isSuperUser) to reach any user action (AAP §0.6.4). Horizontal (per-portal) scoping is applied
// per action below via the ApiControllerBase guards.
[Authorize(Policy = "PortalAdministrator")]
// MIGRATION QA finding F: declare the response contract for OpenAPI/Swagger. Success bodies use the
// { data, meta } envelope (ApiResponse<T>); every error body is an RFC 7807 Problem Details payload
// produced centrally by the exception-handling middleware. 401 is declared once here because every
// action on this authorized resource returns it when the bearer token is missing or invalid; the
// per-action attributes below add the success shape plus the action-specific 400/403/404/409 responses.
// MIGRATION QA finding F: intentionally NO [Produces("application/json")] here. That attribute is an
// MVC result filter that would override the Content-Type of the [ApiController]-produced 400
// ValidationProblemDetails from "application/problem+json" to "application/json", breaking RFC 7807.
// The [ProducesResponseType] attributes alone supply the response schemas to Swagger.
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserService _userService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class.
    /// </summary>
    /// <param name="userService">The application service that implements every user use case.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="userService"/> is <see langword="null"/>.
    /// </exception>
    public UsersController(IUserService userService)
    {
        // MIGRATION: the legacy UserController.vb exposed Public Shared (static) members that reached
        // the database directly through the membership/data providers; the collaborator is now
        // supplied by the built-in DI container as an injected instance (no statics in application
        // code — AAP §0.6.1). Assigning the field here guarantees it is non-null (no CS8618).
        ArgumentNullException.ThrowIfNull(userService);
        _userService = userService;
    }

    /// <summary>
    /// Returns a bounded, server-paginated collection of users, optionally filtered to a single portal.
    /// </summary>
    /// <param name="portalId">
    /// Optional portal identifier. When supplied, only users belonging to that portal are returned;
    /// when omitted, every user is returned.
    /// </param>
    /// <param name="query">
    /// Optional free-text ("All Fields") search term matched across username, email, display name, first
    /// name, and last name (<c>GET /api/users?query=...</c>).
    /// </param>
    /// <param name="filterProperty">
    /// Optional single-field selector (<c>Username</c> or <c>Email</c>, mirroring the SPA's search-type
    /// dropdown). When supplied with <paramref name="filter"/>, matching is restricted to that one field.
    /// </param>
    /// <param name="filter">The term matched against <paramref name="filterProperty"/> when field-specific search is requested.</param>
    /// <param name="page">
    /// Optional 1-based page number (default 1). Values below 1 are normalized to 1.
    /// </param>
    /// <param name="pageSize">
    /// Optional page size (default 50, hard maximum 200). Values above the maximum are clamped so a
    /// single request can never materialize every user (R6 Issue 1 — unbounded lists).
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 200 with the success envelope; <c>data</c> is the current page of users and <c>meta</c>
    /// carries <c>count</c>, <c>page</c>, <c>pageSize</c>, <c>totalCount</c>, and <c>totalPages</c>.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? portalId,
        [FromQuery] string? query,
        [FromQuery] string? filterProperty,
        [FromQuery] string? filter,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        // MIGRATION (R6 Issue 1 — unbounded lists): the previous implementation hydrated the ENTIRE
        // matching user set into memory (all portals, or every user in a portal) before returning it.
        // The endpoint is now bounded — page and pageSize are normalized (default 1/50, hard cap 200) and
        // the data layer applies Skip/Take + a COUNT so a single request can never stream an unbounded set.
        var paging = PaginationParameters.Normalize(page, pageSize);

        // MIGRATION: UserController.GetUsers(portalId) (L685) / the Users.ascx.vb grid feed. The
        // legacy screen was always portal-scoped; the modern list also supports an unfiltered
        // (all-portals) read for host-level administration, selected by the optional portalId query.
        // MIGRATION (authorization — horizontal scoping): a host (super) user may list any portal (or
        // all portals) for host-level administration; a non-host caller is confined to the portal
        // named by its own portalId claim. An explicit cross-portal query is rejected with 403; an
        // unfiltered request is narrowed to the caller's own portal (AAP §0.6.4).
        if (!CallerIsSuperUser())
        {
            var callerPortalId = CallerPortalId();
            if (callerPortalId is null) return ForbiddenProblem("The caller has no portal scope.");
            if (portalId.HasValue && portalId.Value != callerPortalId.Value)
                return ForbiddenProblem($"The caller is not authorized to access resources owned by portal {portalId.Value}.");
            portalId = callerPortalId;
        }

        // MIGRATION: when a search is requested — either a field-specific ?filterProperty=&filter= (legacy
        // GetUsersByUserName / GetUsersByEmail) or a free-text ?query= (name search) — it is performed
        // SERVER-SIDE (AAP §0.7.2 "Search/Filter -> GET /api/users?query=...") via the paged search,
        // honouring the effective portal scope; otherwise the existing per-portal / all-portals list is
        // returned. Every branch is bounded by the same Skip/Take window.
        PagedResult<UserDto> result;
        var fieldSearch = !string.IsNullOrWhiteSpace(filterProperty) && !string.IsNullOrWhiteSpace(filter);
        if (fieldSearch || !string.IsNullOrWhiteSpace(query))
        {
            result = await _userService.SearchPagedAsync(portalId, query, filterProperty, filter, paging.Skip, paging.PageSize, cancellationToken);
        }
        else
        {
            result = portalId.HasValue
                ? await _userService.GetByPortalPagedAsync(portalId.Value, paging.Skip, paging.PageSize, cancellationToken)   // MIGRATION: GetUsers(portalId)
                : await _userService.GetPagedAsync(paging.Skip, paging.PageSize, cancellationToken);
        }
        return PagedEnvelope(result, paging);
    }

    /// <summary>
    /// Returns a single user by identifier.
    /// </summary>
    /// <param name="id">The unique identifier (<c>UserID</c>) of the user to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 200 with the user envelope when found; otherwise HTTP 404.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        // MIGRATION: UserController.GetUser(portalId, userId, isHydrated) (L497) / User.ascx.vb
        // Page_Load (L328) read. A missing user yields 404 (NotFound) instead of the legacy null
        // UserInfo return; the 404 body is produced centrally as RFC 7807 Problem Details by the
        // status-code middleware.
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        if (user is null) return NotFound();

        // MIGRATION (authorization — horizontal scoping): a non-host caller may read a user only when
        // that user belongs to the caller's own portal (AAP §0.6.4).
        var denied = RequirePortalAccess(user.PortalID);
        if (denied is not null) return denied;

        return OkEnvelope(user);
    }

    /// <summary>
    /// Returns a single user within a portal by login name.
    /// </summary>
    /// <param name="portalId">The identifier of the portal that owns the user.</param>
    /// <param name="username">The login name of the user to retrieve.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 200 with the user envelope when found; otherwise HTTP 404.</returns>
    [HttpGet("by-username")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByUsername(
        [FromQuery] int portalId,
        [FromQuery] string username,
        CancellationToken cancellationToken)
    {
        // MIGRATION: UserController.GetUserByName(portalId, username) (L544). Usernames are unique
        // per portal, so the lookup is scoped by both portalId and username; a miss maps to 404. This
        // lives on the dedicated "by-username" segment so it never collides with the "{id:int}"
        // by-id route (the int route constraint rejects the literal "by-username").
        // MIGRATION (authorization — horizontal scoping): the lookup is explicitly portal-scoped, so a
        // non-host caller may query only its own portal (AAP §0.6.4).
        var denied = RequirePortalAccess(portalId);
        if (denied is not null) return denied;

        var user = await _userService.GetByUsernameAsync(portalId, username, cancellationToken);
        return user is null ? NotFound() : OkEnvelope(user);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="dto">The user creation payload.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 201 with the created user envelope and a <c>Location</c> header addressing
    /// <see cref="GetById"/>.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserDto dto,
        CancellationToken cancellationToken)
    {
        // MIGRATION: UserController.CreateUser(ByRef objUser) (L156), reached from User.ascx.vb
        // CreateUser()/cmdAdd (L209→L226), which returned a UserCreateStatus. The service now returns
        // the created projection and the controller emits 201 Created with a Location header pointing
        // at the canonical GET-by-id action (route value key "id" matches the "{id:int}" segment).
        // [ApiController] auto-validates the model and short-circuits with an RFC 7807 400 response
        // (FluentValidation wired in Program.cs) before this body runs when the payload is invalid,
        // so no manual ModelState check is required here.
        // MIGRATION (authorization — horizontal scoping): a non-host caller may create a user only
        // within its own portal (the target portal travels in the DTO) (AAP §0.6.4).
        var denied = RequirePortalAccess(dto.PortalID);
        if (denied is not null) return denied;

        // MIGRATION QA finding K: CreateAsync now returns a CreateUserResult. When the server generated the
        // password (RandomPassword = true) the one-time plaintext is surfaced in the create response's
        // "meta" (new { generatedPassword }) so the provisioned account can be handed off - it is NEVER
        // placed in the persisted/returned UserDto and NEVER returned on a later GET. When the caller
        // supplied the password, GeneratedPassword is null and the base controller's default meta (server
        // timestamp) is used instead.
        var result = await _userService.CreateAsync(dto, cancellationToken);
        object? meta = result.GeneratedPassword is null
            ? null
            : new { generatedPassword = result.GeneratedPassword };
        return CreatedEnvelope(nameof(GetById), new { id = result.User.UserID }, result.User, meta);
    }

    /// <summary>
    /// Updates an existing user's identity and profile.
    /// </summary>
    /// <param name="id">The identifier (<c>UserID</c>) of the user to update.</param>
    /// <param name="dto">The user update payload.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 200 with the updated user envelope when found; otherwise HTTP 404.</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateUserDto dto,
        CancellationToken cancellationToken)
    {
        // MIGRATION: UserController.UpdateUser (L963) / User.ascx.vb cmdUpdate_Click (L361). A null
        // result means the target user does not exist, which maps to 404 instead of the legacy silent
        // no-op on a missing row. Credentials are never altered here — password changes go through the
        // dedicated change-password endpoint and its own DTO.
        // MIGRATION (authorization — horizontal scoping): confirm the target user belongs to the
        // caller's portal before mutating it. The existing row is fetched first so a cross-portal
        // caller is rejected with 403 (not a silent no-op) and a missing row yields 404 (AAP §0.6.4).
        var existing = await _userService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        var denied = RequirePortalAccess(existing.PortalID);
        if (denied is not null) return denied;

        var updated = await _userService.UpdateAsync(id, dto, cancellationToken);
        return updated is null ? NotFound() : OkEnvelope(updated);
    }

    /// <summary>
    /// Permanently deletes a user.
    /// </summary>
    /// <param name="id">The identifier (<c>UserID</c>) of the user to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>HTTP 204 when the user was deleted; HTTP 404 when no user with the given id exists.</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        // MIGRATION: UserController.DeleteUser(ByRef objUser, notify, deleteAdmin) (L200) /
        // User.ascx.vb cmdDelete_Click (L342). The service reports whether a row was removed;
        // true -> 204 No Content (empty body), false -> 404 Not Found.
        // MIGRATION (authorization — horizontal scoping): confirm the target user belongs to the
        // caller's portal before deleting it (AAP §0.6.4). A missing row yields 404; a cross-portal
        // delete is rejected with 403.
        var existing = await _userService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        var denied = RequirePortalAccess(existing.PortalID);
        if (denied is not null) return denied;

        var deleted = await _userService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Changes a user's password.
    /// </summary>
    /// <param name="id">The identifier (<c>UserID</c>) of the user whose password is changing.</param>
    /// <param name="dto">The change-password payload carrying the old and new passwords.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// HTTP 204 when the password was changed; HTTP 404 when the user does not exist; HTTP 409 when the
    /// user exists but the supplied current (old) password does not match.
    /// </returns>
    [HttpPost("{id:int}/change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        // MIGRATION: UserController.ChangePassword(User, old, new) (L103), reached from
        // Password.ascx.vb cmdUpdate_Click (L269→L300) — a static call taking the UserInfo entity and
        // two raw strings. It becomes IUserService.ChangePasswordAsync(userId, dto): the user is
        // addressed by route id and the credentials travel in a dedicated ChangePasswordDto so
        // password material never rides on a general profile-update payload. Hashing/verification
        // (BCrypt) lives in Infrastructure behind the service — never in this controller.
        // MIGRATION (authorization — horizontal scoping): confirm the target user belongs to the
        // caller's portal before changing credentials (AAP §0.6.4). A missing row yields 404; a
        // cross-portal password change is rejected with 403 before the service is reached.
        var existing = await _userService.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound();
        var denied = RequirePortalAccess(existing.PortalID);
        if (denied is not null) return denied;

        // MIGRATION QA finding E: a wrong current (old) password now throws ConflictException (409) INSIDE
        // the service (the target user was already confirmed to exist above), so a bad credential never
        // reaches this line as a false and is no longer conflated with a missing resource. A false here can
        // therefore only mean the row vanished between the pre-fetch and the service call (a genuine
        // not-found race) - correctly surfaced as 404.
        var changed = await _userService.ChangePasswordAsync(id, dto, cancellationToken);
        if (!changed) return NotFound();
        return NoContent();                             // 204 — password changed, no body
    }
}
