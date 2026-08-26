using LibraryManager.Application.Abstractions;

namespace LibraryManager.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
