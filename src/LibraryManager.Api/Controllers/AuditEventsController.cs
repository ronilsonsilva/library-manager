using LibraryManager.Api.Security;
using LibraryManager.Application.Audit;
using LibraryManager.Application.Audit.GetAuditEvents;
using LibraryManager.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.Api.Controllers;

[ApiController]
[Route("audit-events")]
public sealed class AuditEventsController(GetAuditEventsUseCase getAuditEvents) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<ActionResult<PagedResult<AuditEventDto>>> List(
        [FromQuery] int page = Pagination.DefaultPage,
        [FromQuery] int pageSize = Pagination.DefaultPageSize,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await getAuditEvents.ExecuteAsync(
            page,
            pageSize,
            entityType,
            entityId,
            cancellationToken);
        return Ok(result);
    }
}
