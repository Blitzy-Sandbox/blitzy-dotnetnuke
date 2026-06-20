import { formatDate } from '@angular/common';
import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';

/**
 * DateFormatPipe — null-safe, locale-aware date formatter for admin grids and forms.
 *
 * Generalizes the legacy DotNetNuke `FormatExpiryDate(Date)` admin-grid helper
 * (Website/admin/Portal/Portals.ascx.vb ~L250-260; Website/admin/Users/MemberServices.ascx.vb
 * ~L172-185), which returned String.Empty for null/sentinel dates (see Null.IsNull in
 * Library/Components/Shared/Null.vb) and otherwise DateTime.ToShortDateString. This pipe
 * preserves that null-safe short-date behavior (locale short date by default) and adds an
 * optional `format` argument. Pure (the @Pipe default) so it cooperates with OnPush.
 *
 * MIGRATION: the legacy helper localized expired memberships to an "Expired" string
 * (MemberServices.ascx.vb); that branch is feature-specific and intentionally NOT ported
 * into this generic, presentation-only pipe.
 *
 * @example
 * ```html
 * {{ portal.expiryDate | dateFormat }}            <!-- locale short date -->
 * {{ user.createdOnDate | dateFormat:'mediumDate' }}
 * {{ tab.startDate | dateFormat:'yyyy-MM-dd' }}
 * ```
 */
@Pipe({ name: 'dateFormat' })
export class DateFormatPipe implements PipeTransform {
  /**
   * Application locale, resolved from Angular's {@link LOCALE_ID} (typed `string`).
   * Injected via the `inject()` function rather than constructor DI to match the
   * project's standalone convention.
   */
  private readonly locale = inject(LOCALE_ID);

  /**
   * Format a date value as a locale string.
   *
   * Mirrors the legacy `FormatExpiryDate` contract: every value the legacy code treated
   * as "null" (null/undefined/empty) — or that cannot be parsed into a valid date —
   * yields an empty string, and formatting failures degrade to empty rather than
   * throwing (legacy `Try/Catch`).
   *
   * @param value  A {@link Date}, an ISO/parseable date string, an epoch-milliseconds
   *               number, or `null`/`undefined`.
   * @param format An Angular date format — a named alias (e.g. `'shortDate'`,
   *               `'mediumDate'`) or a custom pattern (e.g. `'yyyy-MM-dd'`). Defaults
   *               to `'shortDate'`, which approximates the legacy `ToShortDateString()`.
   * @returns The formatted date string, or `''` for null/undefined/empty/invalid input.
   */
  transform(value: Date | string | number | null | undefined, format = 'shortDate'): string {
    // NULL-SAFE guard (faithful to the legacy default-empty + `Not Null.IsNull` check):
    // null, undefined, and the empty string all map to an empty result.
    if (value === null || value === undefined || value === '') {
      return '';
    }

    // Normalize to a Date. Strings (ISO/parseable) and epoch-ms numbers are coerced via
    // the Date constructor; an existing Date is used as-is.
    const date = value instanceof Date ? value : new Date(value);

    // Invalid-date guard: catches unparseable strings (e.g. 'not-a-date'), whitespace,
    // and other inputs that yield an Invalid Date. Equivalent to the legacy sentinel
    // (NullDate) / null filtering, collapsing to an empty string.
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    try {
      // Local-timezone formatting is the desired default, so no timezone argument is passed.
      return formatDate(date, format, this.locale);
    } catch {
      // Faithful to the legacy Try/Catch: degrade to empty rather than throw
      // (e.g. unregistered locale data or an unparseable format).
      return '';
    }
  }
}
