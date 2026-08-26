using LibraryManager.Api.Contracts.Audit.Responses;
using LibraryManager.Api.Contracts.Common;
using LibraryManager.Api.Security;
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
    public async Task<ActionResult<PagedResponse<AuditEventResponse>>> List(
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
        return Ok(PagedResponse<AuditEventResponse>.From(result, AuditEventResponse.From));
    }
}
