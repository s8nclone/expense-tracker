using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Api.Dtos;
using Microsoft.AspNetCore.Http;

namespace ExpenseTracker.Tests;

public sealed class ExpenseApiTests : IDisposable
{
    private readonly ExpenseTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ExpenseApiTests()
    {
        _factory = new ExpenseTrackerWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task FullCrudLifecycle_PostGetPutDelete_WorksEndToEnd()
    {
        // POST -> 201 Created with Location header
        var createRequest = new CreateExpenseRequest
        {
            Title = "Coffee",
            Description = "Morning espresso",
            Amount = 4.75m,
            Category = "Food",
            Date = new DateOnly(2026, 11, 23)
        };

        var createResponse = await _client.PostAsJsonAsync("/api/expenses", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        var created = await createResponse.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("Coffee", created.Title);
        Assert.Equal("Morning espresso", created.Description);
        Assert.Equal(4.75m, created.Amount);
        Assert.Equal("Food", created.Category);
        Assert.Equal(new DateOnly(2026, 11, 23), created.Date);

        // GET by Location -> 200 OK
        var getResponse = await _client.GetAsync(createResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Coffee", fetched.Title);

        // PUT -> 204 No Content
        var updateRequest = new UpdateExpenseRequest
        {
            Title = "Coffee & Croissant",
            Description = "Breakfast",
            Amount = 8.50m,
            Category = "Dining",
            Date = new DateOnly(2026, 11, 24)
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/expenses/{created.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        // Verify updated fields
        var updated = await _client.GetFromJsonAsync<ExpenseResponse>($"/api/expenses/{created.Id}");
        Assert.NotNull(updated);
        Assert.Equal("Coffee & Croissant", updated.Title);
        Assert.Equal("Breakfast", updated.Description);
        Assert.Equal(8.50m, updated.Amount);
        Assert.Equal("Dining", updated.Category);
        Assert.Equal(new DateOnly(2026, 11, 24), updated.Date);
        Assert.True(updated.UpdatedAtUtc >= created.UpdatedAtUtc);

        // DELETE -> 204 No Content
        var deleteResponse = await _client.DeleteAsync($"/api/expenses/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // GET after DELETE -> 404 Not Found
        var afterDeleteResponse = await _client.GetAsync($"/api/expenses/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task GetExpenses_WithFilteringSortingAndPagination_ReturnsExpectedPage()
    {
        var seed = new[]
        {
            new CreateExpenseRequest { Title = "Groceries", Amount = 65.00m, Category = "Food", Date = new DateOnly(2026, 10, 05) },
            new CreateExpenseRequest { Title = "Bus Pass", Amount = 30.00m, Category = "Transport", Date = new DateOnly(2026, 10, 10) },
            new CreateExpenseRequest { Title = "Dinner", Amount = 42.50m, Category = "Food", Date = new DateOnly(2026, 10, 15) },
            new CreateExpenseRequest { Title = "Snack", Amount = 5.25m, Category = "Food", Date = new DateOnly(2026, 11, 01) },
            new CreateExpenseRequest { Title = "Train", Amount = 120.00m, Category = "Transport", Date = new DateOnly(2026, 11, 05) },
        };

        foreach (var item in seed)
        {
            var resp = await _client.PostAsJsonAsync("/api/expenses", item);
            resp.EnsureSuccessStatusCode();
        }

        // Filter by category (case-insensitive) + sort by amount ascending
        var foodPage = await _client.GetFromJsonAsync<PagedResponse<ExpenseResponse>>(
            "/api/expenses?category=food&sortBy=amount&sortOrder=asc&page=1&pageSize=2");

        Assert.NotNull(foodPage);
        Assert.Equal(3, foodPage.TotalCount);
        Assert.Equal(2, foodPage.TotalPages);
        Assert.Equal(1, foodPage.Page);
        Assert.Equal(2, foodPage.PageSize);
        Assert.Equal(2, foodPage.Items.Count);
        Assert.Equal("Snack", foodPage.Items[0].Title);
        Assert.Equal("Dinner", foodPage.Items[1].Title);

        // Page 2 of same query
        var foodPage2 = await _client.GetFromJsonAsync<PagedResponse<ExpenseResponse>>(
            "/api/expenses?category=food&sortBy=amount&sortOrder=asc&page=2&pageSize=2");

        Assert.NotNull(foodPage2);
        Assert.Single(foodPage2.Items);
        Assert.Equal("Groceries", foodPage2.Items[0].Title);

        // Date range filter
        var octoberPage = await _client.GetFromJsonAsync<PagedResponse<ExpenseResponse>>(
            "/api/expenses?fromDate=2026-10-01&toDate=2026-10-31");

        Assert.NotNull(octoberPage);
        Assert.Equal(3, octoberPage.TotalCount);
    }

    [Fact]
    public async Task GetExpenseSummary_ReturnsTotalsByCategoryAndMonth()
    {
        var seed = new[]
        {
            new CreateExpenseRequest { Title = "Lunch", Amount = 15.00m, Category = "Food", Date = new DateOnly(2026, 10, 12) },
            new CreateExpenseRequest { Title = "Dinner", Amount = 35.00m, Category = "Food", Date = new DateOnly(2026, 11, 02) },
            new CreateExpenseRequest { Title = "Taxi", Amount = 20.00m, Category = "Transport", Date = new DateOnly(2026, 11, 03) },
        };

        foreach (var item in seed)
        {
            var resp = await _client.PostAsJsonAsync("/api/expenses", item);
            resp.EnsureSuccessStatusCode();
        }

        var summary = await _client.GetFromJsonAsync<ExpenseSummaryResponse>("/api/expenses/summary");

        Assert.NotNull(summary);
        Assert.Equal(70.00m, summary.TotalAmount);
        Assert.Equal(3, summary.TotalCount);

        Assert.Equal(2, summary.ByCategory.Count);
        Assert.Equal("Food", summary.ByCategory[0].Category);
        Assert.Equal(50.00m, summary.ByCategory[0].TotalAmount);
        Assert.Equal(2, summary.ByCategory[0].Count);
        Assert.Equal("Transport", summary.ByCategory[1].Category);
        Assert.Equal(20.00m, summary.ByCategory[1].TotalAmount);
        Assert.Equal(1, summary.ByCategory[1].Count);

        Assert.Equal(2, summary.ByMonth.Count);
        Assert.Equal("2026-10", summary.ByMonth[0].Month);
        Assert.Equal(15.00m, summary.ByMonth[0].TotalAmount);
        Assert.Equal("2026-11", summary.ByMonth[1].Month);
        Assert.Equal(55.00m, summary.ByMonth[1].TotalAmount);
    }

    [Fact]
    public async Task CreateExpense_WithInvalidPayload_Returns400ValidationProblem()
    {
        var invalidRequest = new CreateExpenseRequest
        {
            Title = "",
            Amount = -5.00m,
            Category = "",
            Date = null
        };

        var response = await _client.PostAsJsonAsync("/api/expenses", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.True(problem.Errors.ContainsKey("Title"));
        Assert.True(problem.Errors.ContainsKey("Amount"));
        Assert.True(problem.Errors.ContainsKey("Category"));
        Assert.True(problem.Errors.ContainsKey("Date"));
    }

    [Fact]
    public async Task UpdateExpense_WithInvalidPayload_Returns400ValidationProblem()
    {
        var invalidRequest = new UpdateExpenseRequest
        {
            Title = "Valid Title",
            Amount = 0m,
            Category = "Food",
            Date = new DateOnly(2026, 11, 23)
        };

        var response = await _client.PutAsJsonAsync("/api/expenses/1", invalidRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("Amount"));
    }

    [Fact]
    public async Task GetExpenses_WithInvalidQueryParameters_Returns400ValidationProblem()
    {
        var response = await _client.GetAsync("/api/expenses?page=0&pageSize=500&fromDate=2026-12-01&toDate=2026-11-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("page"));
        Assert.True(problem.Errors.ContainsKey("pageSize"));
        Assert.True(problem.Errors.ContainsKey("fromDate"));
    }

    [Fact]
    public async Task NonExistentExpense_GetPutDelete_Returns404NotFound()
    {
        var getResponse = await _client.GetAsync("/api/expenses/9999");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var putResponse = await _client.PutAsJsonAsync("/api/expenses/9999", new UpdateExpenseRequest
        {
            Title = "Does not exist",
            Amount = 10.00m,
            Category = "Other",
            Date = new DateOnly(2026, 11, 23)
        });
        Assert.Equal(HttpStatusCode.NotFound, putResponse.StatusCode);

        var deleteResponse = await _client.DeleteAsync("/api/expenses/9999");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }
}
