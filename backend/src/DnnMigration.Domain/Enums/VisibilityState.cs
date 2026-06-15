namespace DnnMigration.Domain.Enums;

// MIGRATION: Values preserved verbatim from ModuleInfo.vb VisibilityState (VB default 0-based numbering:
// Maximized=0, Minimized=1, None=2). Do NOT reorder to None=0 despite the AAP §0.3.1 layout diagram's
// casual "None/Minimized/Maximized" listing — persisted/compared integer values must remain valid.
public enum VisibilityState
{
    Maximized = 0,
    Minimized = 1,
    None = 2
}
