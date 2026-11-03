namespace ASMARA.API.Common.Exceptions;

public class BusinessRuleException(string message) : AsmaraExceptions(message)
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;

    public override string ErrorCode => "BUSINESS_RULE_VIOLATION";
}