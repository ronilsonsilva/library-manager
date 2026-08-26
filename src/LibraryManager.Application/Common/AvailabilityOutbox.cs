namespace LibraryManager.Application.Common;

public static class AvailabilityOutbox
{
    public const string MessageType = "BookAvailabilityChanged";

    public static string Payload(Guid bookId, string correlationId) =>
        JsonPayload.Serialize(new { bookId, correlationId });
}
