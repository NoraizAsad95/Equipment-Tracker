namespace EquipmentTracker.Api.Services;

// Thrown by services when a rule is broken; one handler maps it to ProblemDetails (AD-8).
public class DomainException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static DomainException Invalid(string message) => new(StatusCodes.Status400BadRequest, message);
    public static DomainException Forbidden(string message) => new(StatusCodes.Status403Forbidden, message);
    public static DomainException NotFound(string message) => new(StatusCodes.Status404NotFound, message);
    public static DomainException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
}
