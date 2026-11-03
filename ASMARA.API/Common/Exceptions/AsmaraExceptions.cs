namespace ASMARA.API.Common.Exceptions;

public abstract class AsmaraExceptions : Exception
{
    protected AsmaraExceptions(string message) : base(message)
    {
    }

    public abstract int StatusCode { get; }
    public abstract string ErrorCode { get; }
}