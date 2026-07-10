import { DatePipe } from '@angular/common';
import { inject, LOCALE_ID, Pipe, PipeTransform } from '@angular/core';

/**
 * Formats an ISO-8601 date string (the wire format of every `core/models`
 * `*Date` / `expiryDate` / `startDate` field) for display in grids, detail
 * views and forms.
 *
 * MIGRATION: replaces the legacy DotNetNuke Web Forms code-behind date helpers
 * `FormatExpiryDate(...)` (Website/admin/Portal/Portals.ascx.vb),
 * `DisplayDate(...)` (Website/admin/Users/Users.ascx.vb) and `FormatDate(...)`
 * (Website/admin/Security/SecurityRoles.ascx.vb), plus the
 * `<asp:BoundColumn DataFormatString="...">` / `<asp:TemplateColumn>` date
 * rendering in the admin list screens. Those helpers returned an empty string
 * for null dates and the culture short-date string otherwise; this pipe
 * preserves that behaviour while delegating locale-aware formatting to
 * Angular's DatePipe.
 */
@Pipe({ name: 'dateFormat' })
export class DateFormatPipe implements PipeTransform {
  // MIGRATION: DNN rendered dates with the server culture's ToShortDateString.
  // Here we delegate to Angular's DatePipe, seeded with the app LOCALE_ID, for
  // locale-aware parity. inject() runs in the pipe's injection context.
  private readonly datePipe = new DatePipe(inject(LOCALE_ID));

  /**
   * @param value    ISO-8601 string, Date, epoch number, or null/undefined.
   * @param format   Angular DatePipe format token (default 'mediumDate';
   *                 pass e.g. 'shortDate' or 'yyyy-MM-dd' for other layouts).
   * @param fallback Text returned for null/empty/invalid input (default '',
   *                 mirroring the legacy helpers' blank rendering).
   * @returns The formatted date, or `fallback` when the input cannot be shown.
   */
  transform(
    value: string | number | Date | null | undefined,
    format = 'mediumDate',
    fallback = '',
  ): string {
    // MIGRATION: grid cells frequently pass null/undefined/empty values; the
    // legacy helpers filtered these out and rendered a blank cell.
    if (value === null || value === undefined) {
      return fallback;
    }
    if (typeof value === 'string' && value.trim() === '') {
      return fallback;
    }

    // MIGRATION: an unset .NET DateTime property (e.g. an unset membership
    // date or a Portal `expiryDate` that was never assigned) is the CLR
    // default `DateTime.MinValue`, which System.Text.Json serialises as
    // "0001-01-01T00:00:00". The legacy DNN date helpers treated such a value
    // as "no date" and rendered a blank cell; without this guard DatePipe
    // would happily format it as "Jan 1, 1" (a confusing, meaningless date in
    // every admin grid). Collapse the sentinel to the fallback for parity.
    if (this.isUnsetDate(value)) {
      return fallback;
    }

    try {
      // DatePipe returns null for unrenderable values and throws for
      // unparseable strings; both collapse to the fallback below.
      return this.datePipe.transform(value, format) ?? fallback;
    } catch {
      return fallback;
    }
  }

  /**
   * Detects the `DateTime.MinValue` sentinel that a never-assigned .NET date
   * property serialises to on the wire.
   *
   * MIGRATION: the check is deliberately conservative — it matches the exact
   * ISO/`.NET` string prefixes the backend emits for the sentinel and, as a
   * general safety net, any value that resolves to a calendar year <= 1.
   * Real domain dates (portal expiry, membership timestamps) are always far
   * later than year 1, so this never suppresses a legitimate value.
   *
   * @param value A non-null, non-empty date value already past the guards above.
   * @returns `true` when the value represents an unset .NET date.
   */
  private isUnsetDate(value: string | number | Date): boolean {
    if (typeof value === 'string') {
      const trimmed = value.trim();
      // Common serialisations of DateTime.MinValue: ISO "0001-01-01T00:00:00"
      // (System.Text.Json / EF Core) and the invariant "1/1/0001" short date.
      if (trimmed.startsWith('0001-01-01') || trimmed.startsWith('1/1/0001')) {
        return true;
      }
    }

    const parsed = value instanceof Date ? value : new Date(value);
    return !Number.isNaN(parsed.getTime()) && parsed.getFullYear() <= 1;
  }
}
