namespace ASMARA.API.Common.Exceptions;

public class UnauthorizedException(string message) : AsmaraExceptions(message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
    public override string ErrorCode => "UNAUTHORIZED";
}