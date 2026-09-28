import { Injectable, inject } from '@angular/core';
import { ContextService } from '../context/context.service';

/**
 * Presentation-only currency/country seam. It never converts, rounds, or
 * persists an amount; business APIs continue to own the currency code and
 * monetary value.
 */
@Injectable({ providedIn: 'root' })
export class CurrencyPresentationService {
  private readonly context = inject(ContextService);

  symbolAssetUrl(currencyCode: string): string | null {
    const presentation = this.presentationFor(currencyCode);
    return presentation?.symbolAssetUrl ?? null;
  }

  symbolText(currencyCode: string): string {
    return this.presentationFor(currencyCode)?.symbolTextFallback ?? currencyCode.trim().toUpperCase();
  }

  formatMoney(amount: number, currencyCode: string, locale = 'en-SA'): string {
    return this.formatMoneyParts(amount, currencyCode, locale)
      .map((part) => part.value)
      .join('');
  }

  formatMoneyParts(
    amount: number,
    currencyCode: string,
    locale = 'en-SA',
    numberOptions: Pick<Intl.NumberFormatOptions, 'minimumFractionDigits' | 'maximumFractionDigits'> = {},
  ): Intl.NumberFormatPart[] {
    const code = currencyCode.trim().toUpperCase();
    if (!Number.isFinite(amount)) {
      return [
        { type: 'integer', value: String(amount) },
        { type: 'literal', value: ' ' },
        { type: 'currency', value: this.symbolText(code) },
      ];
    }

    // Intl currency formatting is deliberately limited to the ISO-shaped
    // path. Configured/non-ISO MESP codes use a localized decimal fallback so
    // a presentation edge case can never break a financial surface.
    if (/^[A-Z]{3}$/.test(code)) {
      try {
        return new Intl.NumberFormat(locale, {
          style: 'currency',
          currency: code,
          currencyDisplay: 'code',
          ...numberOptions,
        }).formatToParts(amount);
      } catch {
        // Fall through to the semantic text fallback below.
      }
    }

    const decimal = new Intl.NumberFormat(locale, {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
      ...numberOptions,
    }).formatToParts(amount);
    return [
      ...decimal,
      { type: 'literal', value: ' ' },
      { type: 'currency', value: this.symbolText(code) },
    ];
  }

  private presentationFor(currencyCode: string) {
    const code = currencyCode.trim().toUpperCase();
    const presentation = this.context.entry()?.currencyPresentation;
    return presentation && presentation.currencyCode.toUpperCase() === code
      ? presentation
      : null;
  }
}
