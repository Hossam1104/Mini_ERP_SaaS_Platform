import { Component } from '@angular/core';

@Component({
  selector: 'app-page-header',
  standalone: true,
  template: `
    <header class="page-header__surface">
      <div class="page-header__body">
        <ng-content select="[page-header-copy]" />
        <div class="page-header__actions"><ng-content select="[page-header-actions]" /></div>
      </div>
    </header>
  `,
})
export class PageHeaderComponent {
}
