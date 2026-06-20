import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  input,
  numberAttribute,
} from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  templateUrl: './loading-spinner.component.html',
  styleUrl: './loading-spinner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoadingSpinnerComponent {
  /** When truthy, the spinner and its `role="status"` live region are rendered. Defaults to `true`. */
  readonly loading = input(true, { transform: booleanAttribute });

  /** Visually-hidden status text announced to assistive technology. Defaults to `'Loading...'`. */
  readonly message = input('Loading...');

  /** Outer diameter of the spinner in pixels. Defaults to `40`. */
  readonly diameter = input(40, { transform: numberAttribute });
}
