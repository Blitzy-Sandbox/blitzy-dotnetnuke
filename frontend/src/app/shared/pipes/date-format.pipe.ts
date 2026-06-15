import { formatDate } from '@angular/common';
import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';

/**
 * DateFormatPipe - null-safe, locale-aware date formatter for admin grids and forms.
 *
 * Generalizes the legacy DotNetNuke `FormatExpiryDate(Date)` admin-grid helper
 * (Website/admin/Portal/Portals.ascx.vb ~L250-260; Website/admin/Users/MemberServices.ascx.vb
 * ~L172-185), which returned `String.Empty` for null/sentinel dates (see `Null.IsNull` in
 * Library/Components/Shared/Null.vb) and otherwise `DateTime.ToShortDateString`. This pipe
 * preserves that null-safe short-date behavior (locale short date by default) and adds an
 * optional `format` argument for richer reuse (e.g. data-table date columns). Pure (the
 * `@Pipe` default) so it cooperates with `ChangeDetectionStrategy.OnPush`.
 *
 * @example
 * // Template usage (the pipe is registered by its name `dateFormat`):
 * //   {{ portal.expiryDate | dateFormat }}              -> locale short date (~ ToShortDateString)
 * //   {{ user.lastLogin    | dateFormat:'mediumDate' }} -> explicit named Angular format
 * //   {{ tab.startDate     | dateFormat:'yyyy-MM-dd' }} -> explicit custom pattern
 *
 * @remarks
 * MIGRATION: the legacy helper wrapped its work in a `Try/Catch` that swallowed errors and
 * returned an empty string; this pipe mirrors that contract by returning `''` for
 * null/undefined/empty/invalid input and by degrading to `''` inside a `try/catch` around
 * {@link formatDate} (it never throws). The MemberServices "Expired" localization branch is
 * intentionally NOT reproduced here - it is feature-specific and belongs in the consuming
 * component, not in this generic, presentation-only pipe.
 */
@Pipe({ name: 'dateFormat' })
export class DateFormatPipe implements PipeTransform {
  /**
   * The active locale id (e.g. `'en-US'`) resolved from the application's `LOCALE_ID` token
   * via the `inject()` function. It drives the locale-aware output of {@link formatDate}.
   */
  private readonly locale = inject(LOCALE_ID);

  /**
   * Format a date value as a locale string.
   *
   * @param value  A `Date`, an ISO/parseable date string, an epoch-millisecond `number`,
   *               `null`, or `undefined`.
   * @param format An Angular date format - either a named alias (`'shortDate'`,
   *               `'mediumDate'`, ...) or a custom pattern such as `'yyyy-MM-dd'`. Defaults to
   *               `'shortDate'` (the closest equivalent of the legacy `ToShortDateString()`).
   * @returns The formatted date string, or `''` for null/undefined/empty/invalid input.
   */
  transform(value: Date | string | number | null | undefined, format = 'shortDate'): string {
    // NULL-SAFE guard FIRST - faithful to the legacy `strDate = String.Empty` default combined
    // with the `If Not Null.IsNull(...)` check: null, undefined and empty string yield ''.
    if (value === null || value === undefined || value === '') {
      return '';
    }

    // Normalize to a Date. Strings (ISO 8601 or other parseable forms) and epoch-millisecond
    // numbers are handed to the Date constructor; an existing Date instance is used as-is.
    const date = value instanceof Date ? value : new Date(value);

    // Invalid-date guard - an unparseable string/number (e.g. 'not-a-date', NaN) yields an
    // "Invalid Date" whose getTime() is NaN. Mirror the legacy null-date filtering with ''.
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    try {
      // Local-timezone, locale-aware formatting. No timezone argument is passed, so the value
      // renders in the host's local time - matching the legacy ToShortDateString() behavior.
      return formatDate(date, format, this.locale);
    } catch {
      // Faithful to the legacy Try/Catch: degrade to empty rather than throw (e.g. an
      // unregistered locale's data or an unparseable format pattern).
      return '';
    }
  }
}
