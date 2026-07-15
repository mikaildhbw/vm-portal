namespace VmPortal.Core.Services;

public class VirtualizationException : Exception
{
    public VirtualizationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
