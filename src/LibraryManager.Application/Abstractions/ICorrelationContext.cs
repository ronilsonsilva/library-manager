namespace LibraryManager.Application.Abstractions;

public interface ICorrelationContext
{
    string CorrelationId { get; }
}
