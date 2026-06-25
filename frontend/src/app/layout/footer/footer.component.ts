// MIGRATION: Net-new static footer (contentinfo landmark). No legacy source — the DNN skin footer is
// out of scope (AAP §0.6.2). Pure presentational shell element; no logic.
import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="app-footer" role="contentinfo">
      <span class="product">DnnMigration Admin &mdash; v{{ version }}</span>
      <span class="copyright">&copy; {{ year }}</span>
    </footer>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .app-footer {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: var(--space-3, 12px) var(--space-4, 16px);
        background: var(--color-surface, #fff);
        border-top: 1px solid var(--color-border, #e0e0e0);
        color: var(--color-text-muted, #666);
        font-size: 0.875rem;
      }
    `,
  ],
})
export class FooterComponent {
  // Version aligns with the backend /health version string ("1.0.0.0", AAP §0.3.4).
  protected readonly version = '1.0.0.0';
  protected readonly year = new Date().getFullYear();
}
