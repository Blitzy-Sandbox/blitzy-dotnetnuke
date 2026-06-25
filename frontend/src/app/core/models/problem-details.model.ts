// MIGRATION: ASP.NET Core built-in Microsoft.AspNetCore.Mvc.ProblemDetails (RFC 7807) -> ProblemDetails.
// No legacy VB equivalent: DNN Web Forms global error handling is replaced by ExceptionHandlingMiddleware (AAP Section 0.7.5).
// Produced by the backend with the error envelope { type, title, status, detail, errors } and parsed by core/interceptors/error.interceptor.ts.
export interface ProblemDetails {
  type?: string;
  title?: string;
  status: number;
  detail?: string | null;
  instance?: string | null;
  // MIGRATION: defensive duality -- `errors` is Record<string, string[]> for [ApiController] model-validation 400s,
  // but a flat string[] for ApiControllerBase Result.Errors failures. The error interceptor MUST handle both forms.
  errors?: Record<string, string[]> | string[];
  // RFC 7807 allows arbitrary extension members (e.g. traceId, correlationId from ProblemDetails.Extensions).
  [key: string]: unknown;
}
