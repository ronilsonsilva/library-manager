# Error codes and HTTP mapping

Language-neutral Result codes. User-facing text is localized at the API from `Error_*` resource keys. Do not put localized strings in `Error.Code`.

## Status mapping

| ErrorType | HTTP | When |
|-----------|------|------|
| (ModelState / `[ApiController]`) | 400 | Body DataAnnotations or Idempotency-Key binder |
| Validation | 400 | Domain/Application input validation Results |
| NotFound | 404 | Named book, User, or loan missing |
| BusinessRule | 422 | Catalog/lending rules (inactive, unavailable, duplicate ISBN/email, duplicate Active loan, non-Active return/cancel, TotalCopies too low) |
| Conflict | 409 | `Idempotency.PayloadMismatch` only |
| Unexpected | 500 | `IExceptionHandler` only; generic body |

Successful `POST /loans` and same-hash replay remain HTTP 201.

## Result codes

See [data-model.md](../data-model.md) for the catalog. Minimum required by this feature:

- `Audit.EntityTypeRequired`, `Audit.EntityIdRequired`
- `Book.NotFound`, `Book.Unavailable`
- `Loan.InvalidState`
- `Idempotency.PayloadMismatch`

## Problem details

### ValidationProblemDetails (HTTP 400, model binding)

- `title`: localized `Problem_Validation_Title`
- `errors`: field → localized messages (`Validation_IdempotencyKey_Required`, `Validation_IdempotencyKey_MaxLength`, `Validation_Title_Required`, …)
- `correlationId` extension
- `Content-Language` matches request culture

### Result problem (404 / 422 / 409 / Domain validation 400)

- `title`: localized category title
- `detail`: localized `Error_*` text
- `code`: `Error.Code` (English)
- `correlationId`

### Unexpected (HTTP 500)

- `title`: localized `Problem_Unexpected_Title`
- `detail`: generic, not `exception.Message`
- `correlationId`
- no stack, SQL, Redis, or connection strings
