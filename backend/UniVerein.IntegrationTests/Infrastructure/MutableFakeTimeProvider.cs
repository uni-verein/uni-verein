namespace UniVerein.IntegrationTests.Infrastructure;

public class MutableFakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public void SetUtcNow(DateTimeOffset value)
    {
        _now = value;
    }

    public override DateTimeOffset GetUtcNow() => _now;
}