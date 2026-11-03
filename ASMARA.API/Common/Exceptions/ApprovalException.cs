namespace ASMARA.API.Common.Exceptions;

public class ApprovalException(string message) : AsmaraExceptions(message)
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
    public override string ErrorCode => "APPROVAL_FAILED";
}