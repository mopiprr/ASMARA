namespace ASMARA.API.Common.Exceptions;

public class TicketStateException(string message) : AsmaraExceptions(message)
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
    public override string ErrorCode => "INVALID_TICKET_STATE";
}