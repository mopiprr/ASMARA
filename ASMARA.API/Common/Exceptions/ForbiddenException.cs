namespace ASMARA.API.Common.Exceptions;

public class ForbiddenException(string message) : AsmaraExceptions(message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;

    public override string ErrorCode => "FORBIDDEN";
}