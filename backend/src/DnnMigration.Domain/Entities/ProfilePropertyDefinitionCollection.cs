using System.Collections.ObjectModel;

namespace DnnMigration.Domain.Entities;

/// <summary>
/// Strongly-typed collection of <c>ProfilePropertyDefinition</c> items. This is the type
/// exposed (read-only) by <c>UserProfile.ProfileProperties</c>.
/// </summary>
// MIGRATION: Converted from VB.NET DotNetNuke.Entities.Profile.ProfilePropertyDefinitionCollection
// (Library/Components/Users/Profile/ProfilePropertyDefinitionCollection.vb), which derived from
// CollectionBase. The modern equivalent derives from System.Collections.ObjectModel.Collection<T>.
// The collection's *contents* remain mutable (items can be added, mirroring the legacy behaviour of
// populating profile properties), while the owning UserProfile.ProfileProperties *property* is
// read-only — you cannot reassign the collection reference, matching the legacy ReadOnly property
// contract. The legacy list-management convenience methods (GetByName, Sort, etc.) are out of scope
// and re-expressed in the Application/Infrastructure layers when the dynamic profile system is
// migrated in a later checkpoint.
public class ProfilePropertyDefinitionCollection : Collection<ProfilePropertyDefinition>
{
}
