namespace LibraryManager.Domain;

public sealed class Error
{
    public Error(string code, ErrorType type, params object[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Type = type;
        Arguments = arguments.Length == 0 ? null : arguments;
    }

    public string Code { get; }

    public ErrorType Type { get; }

    public object[]? Arguments { get; }

    public static Error Validation(string code, params object[] arguments) =>
        new(code, ErrorType.Validation, arguments);

    public static Error NotFound(string code, params object[] arguments) =>
        new(code, ErrorType.NotFound, arguments);

    public static Error BusinessRule(string code, params object[] arguments) =>
        new(code, ErrorType.BusinessRule, arguments);

    public static Error Conflict(string code, params object[] arguments) =>
        new(code, ErrorType.Conflict, arguments);
}
