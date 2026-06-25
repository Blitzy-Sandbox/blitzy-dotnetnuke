// MIGRATION: Net-new, purely presentational busy indicator for the Angular 19 SPA. There is NO
// legacy equivalent — the DotNetNuke Website/admin/** Web Forms UI used full-page server postbacks
// (ViewState) with no client-side loading/busy indicator. Provides internal visual consistency in
// lieu of an external design system (AAP §0.3.7); no @angular/cdk, no Angular animations.
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="loading-spinner" role="status" aria-live="polite">
        <span class="loading-spinner__glyph" aria-hidden="true"></span>
        <span class="sr-only">{{ label() }}</span>
      </div>
    }
  `,
  styles: [`
    .loading-spinner {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: var(--space-2);
    }

    .loading-spinner__glyph {
      box-sizing: border-box;
      display: inline-block;
      width: var(--space-5);
      height: var(--space-5);
      border: 3px solid var(--color-border);
      border-top-color: var(--color-primary);
      border-radius: 50%;
      animation: loading-spinner-spin 0.8s linear infinite;
    }

    @keyframes loading-spinner-spin {
      to {
        transform: rotate(360deg);
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .loading-spinner__glyph {
        animation-duration: 1.6s;
      }
    }
  `],
})
export class LoadingSpinnerComponent {
  /** Controls visibility of the spinner. When false, nothing is rendered. */
  readonly loading = input<boolean>(false);

  /** Accessible, screen-reader-announced label (visually hidden via the global .sr-only class). */
  readonly label = input<string>('Loading…');
}
