using LibraryManager.Api.Contracts.Common;
using LibraryManager.Api.Contracts.Loans.Responses;
using LibraryManager.Api.Contracts.Users.Requests;
using LibraryManager.Api.Contracts.Users.Responses;
using LibraryManager.Api.Security;
using LibraryManager.Application.Common;
using LibraryManager.Application.Users.CreateUser;
using LibraryManager.Application.Users.GetUserLoans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManager.Api.Controllers;

[ApiController]
[Route("users")]
public sealed class UsersController(
    CreateUserUseCase createUser,
    GetUserLoansUseCase getUserLoans) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = LibrarianPolicy.Name)]
    public async Task<ActionResult<UserResponse>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await createUser.ExecuteAsync(request.Name, request.Email, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, UserResponse.From(user));
    }

    [HttpGet("{id:guid}/loans")]
    [Authorize]
    public async Task<ActionResult<PagedResponse<LoanResponse>>> GetLoans(
        Guid id,
        [FromQuery] int page = Pagination.DefaultPage,
        [FromQuery] int pageSize = Pagination.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await getUserLoans.ExecuteAsync(id, page, pageSize, cancellationToken);
        return Ok(PagedResponse<LoanResponse>.From(result, LoanResponse.From));
    }
}
