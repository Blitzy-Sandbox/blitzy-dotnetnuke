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

    try {
      // DatePipe returns null for unrenderable values and throws for
      // unparseable strings; both collapse to the fallback below.
      return this.datePipe.transform(value, format) ?? fallback;
    } catch {
      return fallback;
    }
  }
}
