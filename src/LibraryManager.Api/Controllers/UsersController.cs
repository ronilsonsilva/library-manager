using LibraryManager.Api.Security;
using LibraryManager.Application.Common;
using LibraryManager.Application.Loans;
using LibraryManager.Application.Users;
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
    public async Task<ActionResult<UserDto>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await createUser.ExecuteAsync(request.Name, request.Email, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    [HttpGet("{id:guid}/loans")]
    [Authorize]
    public async Task<ActionResult<PagedResult<LoanDto>>> GetLoans(
        Guid id,
        [FromQuery] int page = Pagination.DefaultPage,
        [FromQuery] int pageSize = Pagination.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await getUserLoans.ExecuteAsync(id, page, pageSize, cancellationToken);
        return Ok(result);
    }
}

public sealed record CreateUserRequest(string Name, string Email);
