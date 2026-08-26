namespace LibraryManager.Application.Common;

public sealed class EntityNotFoundException : Exception
{
    public string EntityName { get; }

    public EntityNotFoundException(string entityName)
        : base($"{entityName} was not found.")
    {
        EntityName = entityName;
    }
}
