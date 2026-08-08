namespace Pnle.Domain.Tutoring;

public sealed class TopicScore
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid UserId { get; private set; }

    public string Topic { get; private set; } = string.Empty;

    public int Correct { get; private set; }

    public int Total { get; private set; }

    public double ScorePercent { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private TopicScore()
    {
        // Required by EF Core.
    }

    public static TopicScore Record(
        Guid userId,
        string topic,
        int correct,
        int total,
        DateTimeOffset now)
    {
        var scorePercent = total == 0
            ? 0
            : Math.Round((double)correct / total * 100, 2);

        return new TopicScore
        {
            UserId = userId,
            Topic = topic,
            Correct = correct,
            Total = total,
            ScorePercent = scorePercent,
            CreatedAtUtc = now
        };
    }
}