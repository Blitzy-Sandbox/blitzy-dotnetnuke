// MIGRATION: Net-new presentational pipe (no 1:1 legacy file). Derived from DNN admin grid boolean
// columns (Website/admin/**) such as user IsApproved/LockedOut/IsSuperUser, role
// IsPublic/AutoAssignment, tab IsVisible/IsDeleted, rendered as human-readable Yes/No.
import { Pipe, PipeTransform } from '@angular/core';

/**
 * Pure presentational pipe that renders a boolean flag as human-readable text. Used for grid/
 * data-table boolean cell display. Nullish input is treated as the negative ("No") case.
 */
@Pipe({ name: 'yesNo', standalone: true })
export class YesNoPipe implements PipeTransform {
  /**
   * @param value The boolean flag to render. Only the literal `true` maps to the affirmative.
   * @param yes Text returned when `value === true`. Defaults to `'Yes'`.
   * @param no Text returned for `false`, `null`, or `undefined`. Defaults to `'No'`.
   * @returns `yes` when the value is strictly `true`; otherwise `no`.
   */
  transform(value: boolean | null | undefined, yes = 'Yes', no = 'No'): string {
    return value === true ? yes : no;
  }
}
