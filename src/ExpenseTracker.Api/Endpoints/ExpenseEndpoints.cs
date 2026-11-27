using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Dtos;
using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static RouteGroupBuilder MapExpenseEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/expenses")
            .WithTags("Expenses");

        group.MapGet("/", GetExpenses)
            .WithName("GetExpenses")
            .WithSummary("List expenses with optional filtering, sorting, and pagination");

        group.MapGet("/{id:int}", GetExpenseById)
            .WithName("GetExpenseById")
            .WithSummary("Get a single expense by ID");

        group.MapPost("/", CreateExpense)
            .WithName("CreateExpense")
            .WithSummary("Create a new expense");

        group.MapPut("/{id:int}", UpdateExpense)
            .WithName("UpdateExpense")
            .WithSummary("Update an existing expense");

        group.MapDelete("/{id:int}", DeleteExpense)
            .WithName("DeleteExpense")
            .WithSummary("Delete an expense");

        group.MapGet("/summary", GetExpenseSummary)
            .WithName("GetExpenseSummary")
            .WithSummary("Get expense totals grouped by category and month");

        return group;
    }

    private static async Task<Results<Ok<PagedResponse<ExpenseResponse>>, ValidationProblem>> GetExpenses(
        ExpenseDbContext db,
        string? category,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? sortBy,
        string? sortOrder,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
            errors["page"] = ["Page must be at least 1."];

        if (pageSize is < 1 or > 100)
            errors["pageSize"] = ["PageSize must be between 1 and 100."];

        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            errors["fromDate"] = ["fromDate cannot be later than toDate."];

        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        IQueryable<Expense> query = db.Expenses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim().ToLower();
            query = query.Where(e => e.Category.ToLower() == normalizedCategory);
        }

        if (fromDate.HasValue)
            query = query.Where(e => e.Date >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.Date <= toDate.Value);

        var descending = !string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        query = sortBy?.ToLowerInvariant() switch
        {
            "amount" => descending
                ? query.OrderByDescending(e => (double)e.Amount).ThenByDescending(e => e.Id)
                : query.OrderBy(e => (double)e.Amount).ThenBy(e => e.Id),
            "category" => descending
                ? query.OrderByDescending(e => e.Category).ThenByDescending(e => e.Id)
                : query.OrderBy(e => e.Category).ThenBy(e => e.Id),
            "title" => descending
                ? query.OrderByDescending(e => e.Title).ThenByDescending(e => e.Id)
                : query.OrderBy(e => e.Title).ThenBy(e => e.Id),
            _ => descending
                ? query.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
                : query.OrderBy(e => e.Date).ThenBy(e => e.Id),
        };

        var totalCount = await query.CountAsync(ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => ExpenseResponse.FromEntity(e))
            .ToListAsync(ct);

        return TypedResults.Ok(new PagedResponse<ExpenseResponse>(items, page, pageSize, totalCount, totalPages));
    }

    private static async Task<Results<Ok<ExpenseResponse>, NotFound>> GetExpenseById(
        int id,
        ExpenseDbContext db,
        CancellationToken ct = default)
    {
        var expense = await db.Expenses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

        return expense is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ExpenseResponse.FromEntity(expense));
    }

    private static async Task<CreatedAtRoute<ExpenseResponse>> CreateExpense(
        CreateExpenseRequest request,
        ExpenseDbContext db,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var expense = new Expense
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Amount = request.Amount!.Value,
            Category = request.Category.Trim(),
            Date = request.Date!.Value,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);

        var response = ExpenseResponse.FromEntity(expense);
        return TypedResults.CreatedAtRoute(response, "GetExpenseById", new { id = expense.Id });
    }

    private static async Task<Results<NoContent, NotFound>> UpdateExpense(
        int id,
        UpdateExpenseRequest request,
        ExpenseDbContext db,
        CancellationToken ct = default)
    {
        var expense = await db.Expenses.FindAsync([id], ct);

        if (expense is null)
            return TypedResults.NotFound();

        expense.Title = request.Title.Trim();
        expense.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        expense.Amount = request.Amount!.Value;
        expense.Category = request.Category.Trim();
        expense.Date = request.Date!.Value;
        expense.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> DeleteExpense(
        int id,
        ExpenseDbContext db,
        CancellationToken ct = default)
    {
        var expense = await db.Expenses.FindAsync([id], ct);

        if (expense is null)
            return TypedResults.NotFound();

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ExpenseSummaryResponse>, ValidationProblem>> GetExpenseSummary(
        ExpenseDbContext db,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct = default)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["fromDate"] = ["fromDate cannot be later than toDate."]
            });
        }

        IQueryable<Expense> query = db.Expenses.AsNoTracking();

        if (fromDate.HasValue)
            query = query.Where(e => e.Date >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.Date <= toDate.Value);

        var rows = await query
            .Select(e => new { e.Amount, e.Category, e.Date })
            .ToListAsync(ct);

        var totalAmount = rows.Sum(r => r.Amount);
        var totalCount = rows.Count;

        var byCategory = rows
            .GroupBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CategorySummaryItem(
                g.First().Category,
                g.Sum(x => x.Amount),
                g.Count()))
            .OrderByDescending(c => c.TotalAmount)
            .ThenBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var byMonth = rows
            .GroupBy(r => $"{r.Date.Year:D4}-{r.Date.Month:D2}")
            .Select(g => new MonthlySummaryItem(
                g.Key,
                g.Sum(x => x.Amount),
                g.Count()))
            .OrderBy(m => m.Month, StringComparer.Ordinal)
            .ToList();

        return TypedResults.Ok(new ExpenseSummaryResponse(totalAmount, totalCount, byCategory, byMonth));
    }
}
