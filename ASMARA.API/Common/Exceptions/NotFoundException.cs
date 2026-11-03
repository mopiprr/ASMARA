namespace ASMARA.API.Common.Exceptions;

public class NotFoundException(string message) : AsmaraExceptions(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;

    public override string ErrorCode => "RESOURCE_NOT_FOUND";
}