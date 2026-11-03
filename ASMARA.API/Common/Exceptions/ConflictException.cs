namespace ASMARA.API.Common.Exceptions;

public class ConflictException(string message) : AsmaraExceptions(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;

    public override string ErrorCode => "RESOURCE_CONFLICT";
}