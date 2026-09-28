import { beforeEach } from 'vitest';

beforeEach(() => {
  localStorage.removeItem('mesp.ui.language');
  localStorage.removeItem('mesp.ui.theme');
  localStorage.removeItem('mesp.ui.dark');
  document.documentElement.lang = 'en';
  document.documentElement.dir = 'ltr';
  document.documentElement.dataset['theme'] = 'sapphire';
  document.documentElement.dataset['colorScheme'] = 'light';
});
