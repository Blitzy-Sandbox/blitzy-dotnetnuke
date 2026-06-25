// MIGRATION: Net-new presentational pipe (no 1:1 legacy file). Derived from DNN admin grid cell
// rendering (Website/admin/**, e.g. Website/admin/Portal/Portals.ascx.vb Format* helpers) where long
// free-text columns (portal Description/FooterText, role Description) are truncated for display.
import { Pipe, PipeTransform } from '@angular/core';

/**
 * Pure presentational pipe that truncates a string to a maximum length, appending a trailing
 * indicator when the value exceeds the limit. Used for grid/data-table cell display.
 */
@Pipe({ name: 'truncate', standalone: true })
export class TruncatePipe implements PipeTransform {
  /**
   * @param value The string to truncate. `null`/`undefined` yield an empty string.
   * @param limit Maximum number of characters to keep before truncating. Defaults to 50.
   * @param trail Suffix appended when the value is truncated. Defaults to the ellipsis character.
   * @returns The original string when its length is within `limit`; otherwise the first `limit`
   *          characters followed by `trail`; or an empty string for nullish input.
   */
  transform(value: string | null | undefined, limit = 50, trail = '…'): string {
    if (value === null || value === undefined) {
      return '';
    }

    return value.length <= limit ? value : value.slice(0, limit) + trail;
  }
}
