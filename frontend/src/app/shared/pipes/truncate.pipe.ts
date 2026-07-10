import { Pipe, PipeTransform } from '@angular/core';

/**
 * Truncates a string to a maximum length and appends an ellipsis (default '…')
 * when the text is cut, for use in fixed-width grid cells and summaries.
 *
 * MIGRATION: net-new shared utility. The legacy DotNetNuke admin DataGrids
 * (e.g. the `Description` column in Website/admin/Security/roles.ascx)
 * constrained long text with fixed column widths / CSS rather than a
 * server-side helper; this pipe provides an explicit, testable equivalent.
 */
@Pipe({ name: 'truncate' })
export class TruncatePipe implements PipeTransform {
  /**
   * @param value        The string to truncate (null/undefined tolerated).
   * @param limit        Max characters before truncation (default 50).
   * @param ellipsis     Suffix appended when truncated (default '…').
   * @param wordBoundary When true, cut at the last whitespace before the limit.
   * @returns The (possibly truncated) string; '' for null/undefined/empty.
   */
  transform(
    value: string | null | undefined,
    limit = 50,
    ellipsis = '…',
    wordBoundary = false,
  ): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }
    // Guard non-positive limits so a negative value never triggers a
    // String.slice tail-cut (slice(0, -n) would drop trailing characters).
    if (limit <= 0) {
      return ellipsis;
    }
    if (value.length <= limit) {
      return value;
    }

    let truncated = value.slice(0, limit);
    if (wordBoundary) {
      const lastSpace = truncated.lastIndexOf(' ');
      if (lastSpace > 0) {
        truncated = truncated.slice(0, lastSpace);
      }
    }
    return truncated + ellipsis;
  }
}
