import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Names of the inline SVG glyphs this component can render. Keep this union in
 * sync with the `@switch` cases in the template.
 */
export type IconName = 'edit' | 'settings' | 'delete' | 'group' | 'roles' | 'menu';

/**
 * icon.component — a tiny standalone presentational component that renders an
 * inline SVG glyph by name.
 *
 * MIGRATION / QA Issue #2: the data-table action buttons previously emitted a
 * Material Icons *ligature* (`<span>edit</span>`), but the application loads no
 * icon web font (AAP §0.3.4 forbids third-party UI libraries, web fonts and
 * any `url(http...)`/CDN reference). With no font present the ligature rendered
 * as the literal word ("edit Edit"). This component replaces that ligature with
 * a self-contained, CSP-safe inline SVG — no network request, no font, no
 * `innerHTML`/sanitiser bypass — so the glyph renders everywhere.
 *
 * The glyph is purely decorative: it is marked `aria-hidden` and `focusable
 * ="false"`, and callers keep supplying a real text label next to it, so the
 * accessible name is unaffected. The SVG uses `stroke: currentColor`, so the
 * icon colour follows the surrounding text colour.
 */
@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './icon.component.html',
  styleUrl: './icon.component.scss',
})
export class IconComponent {
  /** Which glyph to render. */
  readonly name = input.required<IconName>();
}
