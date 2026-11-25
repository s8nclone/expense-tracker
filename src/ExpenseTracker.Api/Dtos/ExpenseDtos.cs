using ExpenseTracker.Api.Models;

namespace ExpenseTracker.Api.Dtos;

public record ExpenseResponse(
    int Id,
    string Title,
    string? Description,
    decimal Amount,
    string Category,
    DateOnly Date,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static ExpenseResponse FromEntity(Expense expense) => new(
        expense.Id,
        expense.Title,
        expense.Description,
        expense.Amount,
        expense.Category,
        expense.Date,
        expense.CreatedAtUtc,
        expense.UpdatedAtUtc);
}

public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record ExpenseSummaryResponse(
    decimal TotalAmount,
    int TotalCount,
    IReadOnlyList<CategorySummaryItem> ByCategory,
    IReadOnlyList<MonthlySummaryItem> ByMonth);

public record CategorySummaryItem(
    string Category,
    decimal TotalAmount,
    int Count);

public record MonthlySummaryItem(
    string Month,
    decimal TotalAmount,
    int Count);
