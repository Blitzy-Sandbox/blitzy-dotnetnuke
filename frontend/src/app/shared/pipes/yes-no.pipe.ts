import { Pipe, PipeTransform } from '@angular/core';

/**
 * Renders a boolean as a human-readable label (default "Yes" / "No").
 *
 * MIGRATION: mirrors the legacy DotNetNuke DataGrid boolean-column rendering —
 * e.g. the "Authorized" column in Website/admin/Users/users.ascx that showed a
 * checked/unchecked image based on `Membership.Approved`. The migrated SPA
 * renders the boolean as text usable in any grid cell or detail field. The
 * default "Yes"/"No" labels can be overridden with localized strings.
 */
@Pipe({ name: 'yesNo' })
export class YesNoPipe implements PipeTransform {
  /**
   * @param value     The boolean to render (null/undefined tolerated).
   * @param trueText  Label for `true` (default 'Yes').
   * @param falseText Label for `false` (default 'No').
   * @param nullText  Label for null/undefined (default '').
   * @returns The label corresponding to `value`.
   */
  transform(
    value: boolean | null | undefined,
    trueText = 'Yes',
    falseText = 'No',
    nullText = '',
  ): string {
    if (value === null || value === undefined) {
      return nullText;
    }
    return value ? trueText : falseText;
  }
}
