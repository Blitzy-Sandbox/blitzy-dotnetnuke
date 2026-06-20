import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-header',
  standalone: true,
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeaderComponent {
  protected readonly auth = inject(AuthService);

  protected readonly displayName = computed<string>(() => {
    const user = this.auth.currentUser();
    if (user === null) {
      return '';
    }

    return user.displayName.trim().length > 0 ? user.displayName : user.username;
  });

  protected logout(): void {
    this.auth.logout();
  }
}
