using AutoMapper;
using AutoMapper.Internal;

namespace DnnMigration.Application.Mapping;

// MIGRATION/SECURITY (QA Checkpoint F5 — finding F-1; AutoMapper CVE-2026-32933 / GHSA-rvv3-g6hj-g44x, HIGH 7.5,
// CWE-674 Uncontrolled Recursion): AutoMapper's core mapping engine recurses through nested object graphs WITHOUT
// enforcing a default maximum depth (default TypeMap.MaxDepth == 0 == unbounded). A deeply nested / cyclic source
// graph (~25,000+ levels) exhausts the thread stack and throws an uncatchable StackOverflowException that
// terminates the whole process. The fix is shipped only in the paid/commercial AutoMapper 15.1.1 / 16.1.1+ lines;
// the maintainer confirmed the free 12.x/13.x/14.x line will NOT be patched, and the project's own guidance is to
// bound recursion with MaxDepth (the engine already uses MaxDepth internally when PreserveReferences is enabled).
//
// Why a code mitigation rather than a version bump or a different mapper:
//   * AAP §0.5.1 PINS AutoMapper.Extensions.Microsoft.DependencyInjection 12.0.1 (the AAP is frozen — rule D1);
//     upgrading to a patched line additionally requires a commercial license and is unavailable in the offline
//     build environment, so the upgrade is declined and documented (MIGRATION_NOTES.md §19.1).
//   * AAP §0.3.3 MANDATES the DTO + AutoMapper pattern, so replacing the mapper (e.g. with Mapperly) is declined.
//   * The advisory-endorsed, zero-cost mitigation — a global MaxDepth bound — is applied here and the pinned
//     version is retained (the scoped <NuGetAuditSuppress> keeps Gate 1 --warnaserror green).
//
// This type contains MAPPING CONFIGURATION ONLY — no business logic, validation, or data access (AAP §0.7.1/§0.7.3).
// The composition root applies it via AddAutoMapper(MappingConfiguration.ApplyRecursionGuard, applicationAssembly),
// and the configuration unit test (AutoMapperConfigurationTests) asserts every discovered TypeMap is bounded.
public static class MappingConfiguration
{
    // MIGRATION/SECURITY (F-1): hard recursion bound applied to EVERY AutoMapper TypeMap. The migrated DTO
    // contracts are flat POCO<->DTO projections (the deepest legitimate graph is a single level), so 8 is far above
    // any real mapping depth yet far below the ~25,000-level stack-exhaustion threshold — it changes no legitimate
    // mapping output while making the uncontrolled-recursion DoS structurally impossible. It also defends any
    // future map that might (incorrectly) introduce a self-referential graph.
    public const int MaxRecursionDepth = 8;

    // MIGRATION/SECURITY (F-1): applies the recursion guard to the mapper configuration. AddAutoMapper(action,
    // assembly) runs this action AFTER the assembly's Profiles have been scanned/added, so ForAllMaps observes
    // every discovered TypeMap and bounds it. ForAllMaps + per-map MaxDepth are reached through AutoMapper's
    // public Internal() API (AutoMapper.Internal namespace); none of these members carry [Obsolete], so they
    // remain safe under Gate 1 (--warnaserror). Calling this directly (rather than relying on a profile-level
    // setting) guarantees coverage regardless of how/when individual profiles are registered.
    public static void ApplyRecursionGuard(IMapperConfigurationExpression configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.Internal().ForAllMaps((_, mappingExpression) =>
            mappingExpression.MaxDepth(MaxRecursionDepth));
    }
}
