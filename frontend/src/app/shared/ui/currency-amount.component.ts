import { Component, Input, inject } from '@angular/core';
import { CurrencyPresentationService } from '../../core/presentation/currency-presentation.service';

@Component({
  selector: 'app-currency-amount',
  standalone: true,
  template: `
    <bdi class="currency-amount">
      @let symbolUrl = symbolAssetUrl;
      @for (part of parts; track $index) {
        @if (part.type === 'currency' && symbolUrl) {
          <span class="currency-symbol-asset" role="img" [attr.aria-label]="currencyLabel" [style.--currency-symbol-url]="'url(' + symbolUrl + ')'"></span>
        } @else {
          <span>{{ part.value }}</span>
        }
      }
    </bdi>
  `,
})
export class CurrencyAmountComponent {
  private readonly presentation = inject(CurrencyPresentationService);

  @Input() amount = 0;
  @Input() currencyCode = '';
  @Input() locale = 'en-SA';

  get parts(): Intl.NumberFormatPart[] {
    return this.presentation.formatMoneyParts(
      this.amount,
      this.currencyCode,
      this.locale,
      { minimumFractionDigits: 2, maximumFractionDigits: 2 },
    );
  }

  get symbolAssetUrl(): string | null {
    return this.presentation.symbolAssetUrl(this.currencyCode);
  }

  get currencyLabel(): string {
    return this.currencyCode.trim().toUpperCase();
  }
}
