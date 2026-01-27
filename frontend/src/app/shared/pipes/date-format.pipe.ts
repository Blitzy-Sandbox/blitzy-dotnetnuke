/**
 * @fileoverview Standalone Angular 19 date formatting pipe for consistent date transformations.
 *
 * MIGRATION NOTE: This pipe replaces the following DNN VB.NET date formatting patterns:
 * - Portals.ascx.vb: FormatExpiryDate function (lines 250-260) that returns DateTime.ToShortDateString or empty
 * - CurrentDate.ascx.vb: Format(date, DateFormat) or ToLongDateString patterns (lines 95-99)
 * - SecurityRoles.ascx.vb: FormatDate function using ToShortDateString
 * - Various admin pages using ToShortDateString/ToLongDateString directly
 *
 * VB.NET to Angular Format Mappings:
 * - ToShortDateString() → 'short' or 'shortDate'
 * - ToLongDateString() → 'long' or 'longDate'
 * - Format(date, pattern) → Custom format string passed directly
 * - Null.IsNull(DateTime) checks → Returns empty string for null/undefined/invalid dates
 */

import { inject, LOCALE_ID, Pipe, PipeTransform } from '@angular/core';
import { DatePipe } from '@angular/common';

/**
 * Predefined format presets that map to Angular DatePipe formats.
 * These correspond to common DNN date display patterns.
 */
type DateFormatPreset = 'short' | 'long' | 'medium' | 'datetime';

/**
 * Maps preset format names to Angular DatePipe format strings.
 */
const FORMAT_PRESETS: Record<DateFormatPreset, string> = {
  /**
   * Short date format - Equivalent to VB.NET ToShortDateString()
   * Example: 1/15/2024 (locale dependent)
   */
  short: 'shortDate',

  /**
   * Long date format - Equivalent to VB.NET ToLongDateString()
   * Example: Monday, January 15, 2024 (locale dependent)
   */
  long: 'longDate',

  /**
   * Medium date format - Balanced between short and long
   * Example: Jan 15, 2024 (locale dependent)
   */
  medium: 'mediumDate',

  /**
   * DateTime format - Date and time combined
   * Example: 1/15/2024, 3:30:00 PM (locale dependent)
   */
  datetime: 'medium'
};

/**
 * Standalone Angular 19 date formatting pipe that provides consistent date transformations
 * across the application. Replaces legacy DNN WebForms date formatting functions.
 *
 * @example
 * ```html
 * <!-- Short date format (default) -->
 * {{ user.createdDate | dateFormat }}
 *
 * <!-- Long date format -->
 * {{ portal.expiryDate | dateFormat:'long' }}
 *
 * <!-- Custom format string -->
 * {{ role.effectiveDate | dateFormat:'MM/dd/yyyy' }}
 *
 * <!-- DateTime format -->
 * {{ audit.timestamp | dateFormat:'datetime' }}
 * ```
 *
 * @usageNotes
 * This pipe gracefully handles null, undefined, empty, and invalid date values by returning
 * an empty string, matching the behavior of DNN's FormatExpiryDate function which checks
 * for Null.IsNull before formatting.
 *
 * Supported format options:
 * - 'short': Equivalent to VB.NET ToShortDateString() - short date format
 * - 'long': Equivalent to VB.NET ToLongDateString() - long date format
 * - 'medium': Medium date format
 * - 'datetime': Date and time combined
 * - Any valid Angular DatePipe format string (e.g., 'MM/dd/yyyy', 'MMMM d, y')
 */
@Pipe({
  name: 'dateFormat',
  standalone: true
})
export class DateFormatPipe implements PipeTransform {
  /**
   * The current application locale obtained via dependency injection.
   * Used to ensure locale-aware date formatting.
   */
  private readonly locale = inject(LOCALE_ID);

  /**
   * Instance of Angular's DatePipe used internally for actual date transformation.
   * Created with the current locale for consistent formatting.
   */
  private readonly datePipe: DatePipe;

  /**
   * Creates an instance of DateFormatPipe with locale-aware DatePipe.
   */
  constructor() {
    this.datePipe = new DatePipe(this.locale);
  }

  /**
   * Transforms a date value into a formatted string representation.
   *
   * MIGRATION: This method replaces the following DNN VB.NET patterns:
   * - FormatExpiryDate function that checks Null.IsNull and returns ToShortDateString
   * - FormatDate function using ToShortDateString
   * - Format(date, DateFormat) patterns for custom formatting
   *
   * @param value - The date value to format. Accepts Date objects, date strings,
   *                numbers (timestamps), null, or undefined.
   * @param format - Optional format specification. Can be:
   *                 - 'short': Short date format (default, equivalent to ToShortDateString)
   *                 - 'long': Long date format (equivalent to ToLongDateString)
   *                 - 'medium': Medium date format
   *                 - 'datetime': Date and time combined
   *                 - Custom format string (e.g., 'MM/dd/yyyy')
   * @returns The formatted date string, or an empty string if the input is null,
   *          undefined, empty, or an invalid date.
   *
   * @example
   * ```typescript
   * // Using preset formats
   * transform(new Date(), 'short')    // '1/15/2024'
   * transform(new Date(), 'long')     // 'Monday, January 15, 2024'
   * transform(new Date(), 'datetime') // '1/15/2024, 3:30:00 PM'
   *
   * // Using custom format strings
   * transform(new Date(), 'MM/dd/yyyy')  // '01/15/2024'
   * transform(new Date(), 'MMMM d, y')   // 'January 15, 2024'
   *
   * // Handling null/invalid values (matches DNN Null.IsNull pattern)
   * transform(null)           // ''
   * transform(undefined)      // ''
   * transform('')             // ''
   * transform('invalid date') // ''
   * ```
   */
  transform(
    value: Date | string | number | null | undefined,
    format: DateFormatPreset | string = 'short'
  ): string {
    // MIGRATION: Matches DNN FormatExpiryDate pattern - Null.IsNull check
    // Returns empty string for null, undefined, or empty values
    if (value === null || value === undefined) {
      return '';
    }

    // Handle empty string input
    if (typeof value === 'string' && value.trim() === '') {
      return '';
    }

    // Resolve the format string from presets or use the provided format directly
    const resolvedFormat = this.resolveFormat(format);

    try {
      // Attempt to parse and validate the date
      const dateValue = this.parseDate(value);

      // Return empty string for invalid dates (matches DNN null handling behavior)
      if (!this.isValidDate(dateValue)) {
        return '';
      }

      // Use Angular's DatePipe for the actual formatting
      // MIGRATION: datePipe.transform() replaces VB.NET DateTime.ToShortDateString(),
      // ToLongDateString(), and Format(date, pattern) calls
      const formattedDate = this.datePipe.transform(dateValue, resolvedFormat);

      // Return empty string if DatePipe returns null (invalid format or date)
      return formattedDate ?? '';
    } catch {
      // MIGRATION: Matches DNN try-catch pattern in FormatExpiryDate
      // Return empty string on any parsing or formatting error
      return '';
    }
  }

  /**
   * Resolves a format string from a preset name or returns the custom format as-is.
   *
   * @param format - The format preset name or custom format string.
   * @returns The resolved Angular DatePipe format string.
   */
  private resolveFormat(format: DateFormatPreset | string): string {
    // Check if the format is a known preset
    if (format in FORMAT_PRESETS) {
      return FORMAT_PRESETS[format as DateFormatPreset];
    }

    // Return the format string as-is for custom formats
    // This allows passing any valid Angular DatePipe format string
    // Examples: 'MM/dd/yyyy', 'MMMM d, y', 'yyyy-MM-dd HH:mm:ss'
    return format;
  }

  /**
   * Parses a date value into a Date object.
   *
   * @param value - The value to parse (Date, string, or number).
   * @returns A Date object representing the input value.
   */
  private parseDate(value: Date | string | number): Date {
    // If already a Date object, return as-is
    if (value instanceof Date) {
      return value;
    }

    // If number (timestamp), convert to Date
    if (typeof value === 'number') {
      return new Date(value);
    }

    // If string, parse into Date
    // Note: The Date constructor handles ISO 8601 strings and many common formats
    return new Date(value);
  }

  /**
   * Validates whether a Date object represents a valid date.
   *
   * MIGRATION: This validation matches the DNN Null.IsNull pattern where
   * null dates (often represented as DateTime.MinValue or specific sentinel values)
   * should be displayed as empty strings.
   *
   * @param date - The Date object to validate.
   * @returns True if the date is valid, false otherwise.
   */
  private isValidDate(date: Date): boolean {
    // Check if the Date object is valid (not NaN)
    // Invalid dates created from invalid strings will have getTime() === NaN
    return date instanceof Date && !isNaN(date.getTime());
  }
}
