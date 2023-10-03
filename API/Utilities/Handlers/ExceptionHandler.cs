namespace API.Utilities.Handlers;

public class ExceptionHandler : Exception
{
    public ExceptionHandler(string message) : base(message) { }
}
