namespace ExpenseTracker.Api.Models;

public class Expense
{
    public int Id { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public decimal Amount { get; set; }

    public required string Category { get; set; }

    public DateOnly Date { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
