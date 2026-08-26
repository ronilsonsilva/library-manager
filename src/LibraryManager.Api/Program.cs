using LibraryManager.Api.Errors;
using LibraryManager.Api.Health;
using LibraryManager.Api.Middleware;
using LibraryManager.Api.OpenApi;
using LibraryManager.Api.Security;
using LibraryManager.Application.Abstractions;
using LibraryManager.Application.Books.CreateBook;
using LibraryManager.Application.Books.DeactivateBook;
using LibraryManager.Application.Books.GetBook;
using LibraryManager.Application.Books.ListBooks;
using LibraryManager.Application.Books.UpdateBook;
using LibraryManager.Application.Loans.CreateLoan;
using LibraryManager.Application.Users.CreateUser;
using LibraryManager.Application.Users.GetUserLoans;
using LibraryManager.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddLibraryManagerAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddLibraryManagerSwagger(builder.Configuration);
builder.Services.AddLibraryManagerInfrastructure(builder.Configuration);
builder.Services.AddScoped<CorrelationContext>();
builder.Services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContext>());
builder.Services.AddScoped<CreateBookUseCase>();
builder.Services.AddScoped<GetBookUseCase>();
builder.Services.AddScoped<ListBooksUseCase>();
builder.Services.AddScoped<UpdateBookUseCase>();
builder.Services.AddScoped<DeactivateBookUseCase>();
builder.Services.AddScoped<CreateUserUseCase>();
builder.Services.AddScoped<GetUserLoansUseCase>();
builder.Services.AddScoped<CreateLoanUseCase>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseLibraryManagerSwagger();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthEndpoints();
app.MapSecurityProbes();

app.Run();

public partial class Program;
