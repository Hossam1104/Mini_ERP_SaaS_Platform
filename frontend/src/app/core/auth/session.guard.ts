import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { ContextService } from '../context/context.service';

export const sessionGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const context = inject(ContextService);
  const router = inject(Router);
  return auth.ensureSession().then(async (authenticated) => {
    if (!authenticated) {
      return router.createUrlTree(['/login']);
    }
    if (auth.developmentBypassActive() && !auth.session()?.selectedContextId) {
      const entry = await context.loadEntry();
      if (entry?.entryMode === 'CommonHost') {
        return router.createUrlTree(['/login']);
      }
    }
    return true;
  });
};
