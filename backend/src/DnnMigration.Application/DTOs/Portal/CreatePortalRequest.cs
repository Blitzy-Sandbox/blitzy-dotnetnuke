// <copyright file="CreatePortalRequest.cs" company="DnnMigration">
// Copyright (c) DnnMigration. All rights reserved.
// Licensed under the MIT License.
// </copyright>

// MIGRATION: Derived from Website/admin/Portal/Signup.ascx.vb cmdUpdate_Click handler (line 274)
// MIGRATION: Original CreatePortal method signature:
// CreatePortal(txtTitle.Text, txtFirstName.Text, txtLastName.Text, txtUsername.Text, txtPassword.Text, 
//              txtEmail.Text, txtDescription.Text, txtKeyWords.Text, HostMapPath, strTemplateFile, 
//              HomeDir, strPortalAlias, strServerPath, strChildPath, blnChild)

using System.ComponentModel.DataAnnotations;

namespace DnnMigration.Application.DTOs.Portal;

/// <summary>
/// Represents a request to create a new portal.
/// Contains all fields required for portal creation including portal metadata,
/// administrator credentials, and configuration settings.
/// </summary>
/// <remarks>
/// This DTO is derived from the legacy Signup.ascx.vb portal creation workflow.
/// Server-side parameters (HostMapPath, strServerPath, strChildPath) are resolved
/// internally and not exposed in the API contract.
/// </remarks>
/// <param name="PortalAlias">
/// The domain/URL alias for the portal (e.g., "mysite.domain.com" or "domain.com/mysite" for child portals).
/// This is derived from txtPortalName.Text in the original implementation.
/// </param>
/// <param name="Title">
/// The display title for the portal shown in browser title bar and headers.
/// Derived from txtTitle.Text in the original implementation.
/// </param>
/// <param name="Description">
/// Optional description of the portal used for SEO and metadata purposes.
/// Derived from txtDescription.Text in the original implementation.
/// </param>
/// <param name="KeyWords">
/// Optional comma-separated SEO keywords for the portal.
/// Derived from txtKeyWords.Text in the original implementation.
/// </param>
/// <param name="FirstName">
/// First name of the portal administrator user to be created.
/// Derived from txtFirstName.Text in the original implementation.
/// </param>
/// <param name="LastName">
/// Last name of the portal administrator user to be created.
/// Derived from txtLastName.Text in the original implementation.
/// </param>
/// <param name="Username">
/// Username for the portal administrator account.
/// Derived from txtUsername.Text in the original implementation.
/// </param>
/// <param name="Password">
/// Password for the portal administrator account.
/// Derived from txtPassword.Text in the original implementation.
/// </param>
/// <param name="Email">
/// Email address for the portal administrator account.
/// Derived from txtEmail.Text in the original implementation.
/// </param>
/// <param name="Template">
/// Optional template file name to use for portal creation (without .template extension).
/// Derived from cboTemplate selection in the original implementation.
/// If not specified, default template will be used.
/// </param>
/// <param name="HomeDirectory">
/// Optional custom home directory path for the portal.
/// Derived from txtHomeDirectory.Text in the original implementation.
/// Defaults to "Portals/[PortalID]" if not specified.
/// </param>
/// <param name="IsChildPortal">
/// Indicates whether this is a child portal (subdirectory) or a parent portal (separate domain).
/// Derived from optType.SelectedValue == "C" in the original implementation.
/// Child portals create a subdirectory under the main site, parent portals use separate domain aliases.
/// </param>
public record CreatePortalRequest(
    [property: Required(ErrorMessage = "Portal alias is required.")]
    [property: StringLength(200, MinimumLength = 1, ErrorMessage = "Portal alias must be between 1 and 200 characters.")]
    string PortalAlias,

    [property: Required(ErrorMessage = "Portal title is required.")]
    [property: StringLength(128, MinimumLength = 1, ErrorMessage = "Portal title must be between 1 and 128 characters.")]
    string Title,

    [property: StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    string? Description,

    [property: StringLength(500, ErrorMessage = "Keywords cannot exceed 500 characters.")]
    string? KeyWords,

    [property: Required(ErrorMessage = "Administrator first name is required.")]
    [property: StringLength(50, MinimumLength = 1, ErrorMessage = "First name must be between 1 and 50 characters.")]
    string FirstName,

    [property: Required(ErrorMessage = "Administrator last name is required.")]
    [property: StringLength(50, MinimumLength = 1, ErrorMessage = "Last name must be between 1 and 50 characters.")]
    string LastName,

    [property: Required(ErrorMessage = "Administrator username is required.")]
    [property: StringLength(100, MinimumLength = 1, ErrorMessage = "Username must be between 1 and 100 characters.")]
    string Username,

    [property: Required(ErrorMessage = "Administrator password is required.")]
    [property: StringLength(128, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 128 characters.")]
    string Password,

    [property: Required(ErrorMessage = "Administrator email is required.")]
    [property: EmailAddress(ErrorMessage = "A valid email address is required.")]
    [property: StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    string Email,

    [property: StringLength(200, ErrorMessage = "Template name cannot exceed 200 characters.")]
    string? Template = null,

    [property: StringLength(200, ErrorMessage = "Home directory cannot exceed 200 characters.")]
    string? HomeDirectory = null,

    bool IsChildPortal = false
);
