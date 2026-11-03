namespace ASMARA.API.Common.Exceptions;

public class InsufficientStockException(string message) : AsmaraExceptions(message)
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
    public override string ErrorCode => "INSUFFICIENT_STOCK";
}