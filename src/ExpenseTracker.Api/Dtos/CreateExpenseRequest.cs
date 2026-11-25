using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Api.Dtos;

public class CreateExpenseRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Title is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(
        typeof(decimal),
        "0.01",
        "999999999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Amount must be greater than 0.")]
    public decimal? Amount { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Category is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Category must be between 1 and 50 characters.")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Date is required.")]
    public DateOnly? Date { get; set; }
}
