import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * `LoadingSpinnerComponent` — a **presentation-only**, accessible busy indicator
 * (animated spinner) rendered while a parent component has a pending asynchronous
 * operation (typically an in-flight HTTP call) in the migrated Angular 19
 * standalone SPA (`dnn-migration`).
 *
 * It is a leaf building block consumed by `features/*` list and detail/form
 * screens and by `layout/*`. It owns **no** state, performs **no** HTTP, injects
 * **no** services, and contains **no** business logic; the busy flag is provided
 * by the parent via the {@link loading} signal input (AAP §0.7.1).
 *
 * ### Behaviour
 * - When {@link loading} is `false` (the default) the component renders nothing
 *   (guarded by the built-in `@if` control-flow block), so it is safe to leave
 *   permanently in a template and simply bind its `loading` input.
 * - When {@link loading} is `true` it renders a centered spinner, an optional
 *   visible {@link message}, and an always-present visually-hidden status text
 *   so assistive technology announces the busy state.
 *
 * ### Accessibility (AAP §0.3.4)
 * - The container is an ARIA live region (`role="status"`, `aria-live="polite"`)
 *   and reflects the busy state via `[attr.aria-busy]`.
 * - The animated graphic is decorative and hidden from assistive tech
 *   (`aria-hidden="true"`); the visible message is likewise hidden to avoid a
 *   double announcement, while a visually-hidden node carries the announced text.
 * - Respects `prefers-reduced-motion` by slowing the rotation.
 *
 * @example Inline spinner driven by a component signal
 * ```html
 * <app-loading-spinner [loading]="isSaving()" message="Saving portal…" />
 * ```
 *
 * @example Overlay spinner covering a positioned container
 * ```html
 * <section style="position: relative">
 *   <app-loading-spinner [loading]="isLoading()" [overlay]="true" size="large" />
 *   <!-- list/table content -->
 * </section>
 * ```
 */
// MIGRATION: No single legacy source control. The DNN Web Forms admin screens under
// Website/admin/** presented an implicit postback / IClientAPICallbackEventHandler ("UpdateProgress"-style)
// "please wait" affordance during server round-trips. In the stateless async HTTP + Signals model,
// loading is now an EXPLICIT, feature-owned boolean surfaced through this presentation-only component.
@Component({
  selector: 'app-loading-spinner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div
        class="loading-spinner"
        [class.loading-spinner--overlay]="overlay()"
        [class.loading-spinner--small]="size() === 'small'"
        [class.loading-spinner--large]="size() === 'large'"
        role="status"
        aria-live="polite"
        [attr.aria-busy]="loading()"
      >
        <span class="loading-spinner__circle" aria-hidden="true"></span>

        @if (message()) {
          <span class="loading-spinner__message" aria-hidden="true">{{ message() }}</span>
        }

        <!-- Visually-hidden text so screen readers announce the busy state even
             when no visible message is supplied. -->
        <span class="loading-spinner__sr-only">{{ message() || defaultLabel }}</span>
      </div>
    }
  `,
  styles: [
    `
      /* Custom properties carry sensible fallbacks so the spinner renders
         correctly even before the global theme tokens in src/styles.scss load. */
      :host {
        display: inline-flex;
      }

      .loading-spinner {
        display: inline-flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: 0.5rem;

        &--overlay {
          position: absolute;
          inset: 0;
          background: var(--loading-spinner-overlay-bg, rgba(255, 255, 255, 0.7));
          z-index: var(--loading-spinner-overlay-z, 1000);
        }

        &__circle {
          width: var(--loading-spinner-size, 2.5rem);
          height: var(--loading-spinner-size, 2.5rem);
          border: var(--loading-spinner-thickness, 4px) solid
            var(--loading-spinner-track-color, rgba(0, 0, 0, 0.1));
          border-top-color: var(--loading-spinner-color, var(--color-primary, #2563eb));
          border-radius: 50%;
          animation: loading-spinner-rotate 0.8s linear infinite;
        }

        &--small &__circle {
          --loading-spinner-size: 1.25rem;
          --loading-spinner-thickness: 3px;
        }

        &--large &__circle {
          --loading-spinner-size: 4rem;
          --loading-spinner-thickness: 6px;
        }

        &__message {
          font-size: var(--font-size-sm, 0.875rem);
          color: var(--loading-spinner-color, var(--color-primary, #2563eb));
        }

        /* Visually hidden but available to assistive technology. */
        &__sr-only {
          position: absolute;
          width: 1px;
          height: 1px;
          padding: 0;
          margin: -1px;
          overflow: hidden;
          clip: rect(0, 0, 0, 0);
          white-space: nowrap;
          border: 0;
        }
      }

      /* Namespaced so the component's scoped selectors and this global keyframe
         name never collide with sibling components' animations. */
      @keyframes loading-spinner-rotate {
        to {
          transform: rotate(360deg);
        }
      }

      /* Respect the user's reduced-motion preference (accessibility best practice):
         keep an indication of activity but slow the rotation dramatically. */
      @media (prefers-reduced-motion: reduce) {
        .loading-spinner__circle {
          animation-duration: 2s;
        }
      }
    `,
  ],
})
export class LoadingSpinnerComponent {
  /**
   * Whether the spinner is shown. When `false` (default) the component renders
   * nothing, so an unbound `<app-loading-spinner />` is inert and safe.
   */
  readonly loading = input<boolean>(false);

  /**
   * Optional visible label rendered beneath the spinner (e.g. `"Saving portal…"`).
   * An empty string (default) renders no visible label; the screen-reader text
   * then falls back to {@link defaultLabel}.
   */
  readonly message = input<string>('');

  /**
   * Spinner dimension preset. Applied via a CSS modifier class; `'medium'`
   * (default) uses the base custom-property sizing.
   */
  readonly size = input<'small' | 'medium' | 'large'>('medium');

  /**
   * When `true`, the spinner is absolutely positioned and covers the nearest
   * positioned ancestor as a centered translucent overlay. When `false`
   * (default) it renders inline within normal document flow.
   */
  readonly overlay = input<boolean>(false);

  /**
   * Default screen-reader announcement used when {@link message} is empty. Kept
   * `protected` so the component's own template can read it while remaining
   * outside the public (parent-bindable) API surface.
   */
  protected readonly defaultLabel = 'Loading…';
}
