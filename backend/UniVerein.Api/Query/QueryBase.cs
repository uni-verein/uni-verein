namespace UniVerein.Api.Query;

public class QueryBase
{
    public int Limit { get; set; } = 100;
    public int Offset { get; set; } = 0;
}