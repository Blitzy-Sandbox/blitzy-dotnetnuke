// MIGRATION: Net-new functional error interceptor. There is NO direct legacy analog -- DNN handled errors via Web
// Forms global error handling (Global.asax / page-level try-catch), and unauthenticated access was redirected to the
// portal Login tab by ASP.NET Forms authentication (PortalSecurity.SignOut() expired the auth cookies). Both are
// replaced here by: (1) parsing the backend RFC 7807 ProblemDetails envelope produced by ExceptionHandlingMiddleware
// (AAP Section 0.7.5), and (2) a JWT 401 -> refresh -> retry-once -> logout/redirect flow that supersedes the
// Forms-auth redirect.
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { environment } from '../../../environments/environment';
import type { ProblemDetails } from '../models/problem-details.model';

/**
 * Parsed projection of an RFC 7807 ProblemDetails body.
 * `messages` are flat user-facing strings (title/detail + every error entry); `fieldErrors` is the per-field
 * validation map (populated only for the dictionary form of `errors`).
 */
export interface ParsedProblem {
  messages: string[];
  fieldErrors: Record<string, string[]>;
}

// MIGRATION: defensive `errors` DUALITY (AAP Section 0.7.5 + core/models/problem-details.model.ts). The backend emits
// `errors` as Record<string, string[]> for [ApiController] model-validation 400s, but as a flat string[] for
// ApiControllerBase Result.Errors failures. BOTH shapes are handled here without throwing. Exported so it can be
// unit-tested directly (Gate 4).
export function parseProblemDetails(body: unknown): ParsedProblem {
  const messages: string[] = [];
  const fieldErrors: Record<string, string[]> = {};

  if (typeof body === 'object' && body !== null) {
    const problem = body as ProblemDetails;

    if (typeof problem.title === 'string' && problem.title.length > 0) {
      messages.push(problem.title);
    }
    if (typeof problem.detail === 'string' && problem.detail.length > 0) {
      messages.push(problem.detail);
    }

    const errors = problem.errors;
    if (Array.isArray(errors)) {
      // ApiControllerBase Result.Errors -> flat string[].
      messages.push(...errors);
    } else if (typeof errors === 'object' && errors !== null) {
      // [ApiController] model-validation -> Record<string, string[]>.
      for (const field of Object.keys(errors)) {
        const fieldMessages = errors[field];
        if (Array.isArray(fieldMessages)) {
          fieldErrors[field] = fieldMessages;
          messages.push(...fieldMessages);
        }
      }
    }
  }

  return { messages, fieldErrors };
}

// MIGRATION: frontend error logging sink (AAP Section 0.1.2 permits logging frontend errors to the API/an external
// sink). The Authorization header / JWT is NEVER logged. AAP Section 0.7.6 additionally requires that logging
// exclude sensitive data: the parsed ProblemDetails `messages` (title/detail + every error entry) and the
// `fieldErrors` VALUES can echo user-entered input (e.g. a validation message quoting an email/username), i.e.
// potential PII. So those payloads are REDACTED in production and only the non-sensitive envelope is logged --
// status, url, the NAMES of the fields that failed validation (schema metadata, never their values), and a count
// of messages. In development the full parsed detail is logged for debuggability. Swap console for an HTTP/Sentry
// sink in production if richer (server-side, access-controlled) telemetry is desired.
function logError(error: HttpErrorResponse): void {
  const parsed = parseProblemDetails(error.error);
  if (environment.production) {
    console.error('[API error]', {
      status: error.status,
      url: error.url,
      // Field NAMES only (e.g. "email", "username") -- never the message text or submitted values.
      failedFields: Object.keys(parsed.fieldErrors),
      messageCount: parsed.messages.length,
    });
    return;
  }
  console.error('[API error]', {
    status: error.status,
    url: error.url,
    messages: parsed.messages,
    fieldErrors: parsed.fieldErrors,
  });
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // MIGRATION: auth-endpoint recursion guard. login/refresh/logout/me all live under `${apiUrl}/auth`; a 401 on these
  // (login/refresh failures actually return 400, not 401) must NEVER trigger another refresh, or the refresh-retry
  // flow would recurse infinitely.
  const isAuthEndpoint = req.url.startsWith(`${environment.apiUrl}/auth`);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // MIGRATION: 401 -> refresh -> retry-once -> logout/redirect (replaces the Forms-auth redirect to the Login tab).
      if (error.status === 401 && !isAuthEndpoint) {
        return authService.refresh().pipe(
          switchMap(() => {
            // MIGRATION: re-tokenize the retried request with the freshly-rotated access token. tokenInterceptor runs
            // BEFORE this interceptor in the chain, so the retry is re-cloned here with the new token rather than
            // re-entering tokenInterceptor.
            const newToken = authService.accessToken();
            const retried =
              newToken !== null
                ? req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } })
                : req;
            return next(retried);
          }),
          catchError((refreshError: unknown) => {
            // Refresh failed OR the single retry failed again (e.g. a second 401) -> terminate the session and
            // redirect to the login route. logout() also clears the in-memory tokens server- and client-side.
            authService.logout().subscribe({ error: () => undefined });
            void router.navigate(['/auth/login']);
            return throwError(() => refreshError);
          }),
        );
      }

      // MIGRATION: surface every non-401 error (400/403/404/409/422/429/5xx) as parsed RFC 7807 messages for the
      // UI/logging, then re-throw the original HttpErrorResponse so feature services/components can react. The
      // ProblemDetails body remains available on `error.error`. NEVER log tokens or sensitive data.
      logError(error);
      return throwError(() => error);
    }),
  );
};
