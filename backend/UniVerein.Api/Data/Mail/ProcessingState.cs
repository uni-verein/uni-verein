using UniVerein.Api.ApiResults;

namespace UniVerein.Api.Data.Mail;

public class ProcessingState
{
    private int Processed { get; set; }
    private int Successful { get; set; }
    private int Failed { get; set; }

    private readonly object _lock = new();

    public (int processed, int successful, int failed) RegisterResult(EmailResult result)
    {
        lock (_lock)
        {
            Processed++;
            if (result.Success)
                Successful++;
            else
                Failed++;

            return (Processed, Successful, Failed);
        }
    }
}