using System.Security.Claims;
using BudgetTracker.Api.AntiForgery;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Features.Transactions.Import.Processing;
using BudgetTracker.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Api.Features.Transactions.Import;

public static class TransactionImportApi
{
	public static IEndpointRouteBuilder MapTransactionImportEndpoint(this IEndpointRouteBuilder routes)
	{
		routes.MapPost("/import", ImportAsync)
			.DisableAntiforgery()
			.AddEndpointFilter<ConditionalAntiforgeryFilter>()
			.WithName("ImportTransactions")
			.WithSummary("Import transactions from a CSV file")
			.Produces<ImportResult>()
			.Accepts<IFormFile>("multipart/form-data");

		return routes;
	}

	private static async Task<Results<Ok<ImportResult>, BadRequest<string>>> ImportAsync(
		IFormFile file, [FromForm] string account,
		BudgetTrackerContext db, ClaimsPrincipal claimsPrincipal, CsvImporter csvImporter)
	{
		var validationError = ValidateFileInput(file, account);
		if (validationError != null)
			return validationError;

		var userId = claimsPrincipal.GetUserId();
		try
		{
			using var stream = file.OpenReadStream();
			var (importResult, transactions) = await csvImporter.ParseCsvAsync(stream, file.FileName, userId, account);

			if (transactions.Count > 0)
			{
				await db.Transactions.AddRangeAsync(transactions);
				await db.SaveChangesAsync();
			}

			return TypedResults.Ok(importResult);
		}
		catch (Exception ex)
		{
			return TypedResults.BadRequest($"Import failed: {ex.Message}");
		}
	}

	private static BadRequest<string>? ValidateFileInput(IFormFile file, string account)
	{
		if (file is null || file.Length == 0)
			return TypedResults.BadRequest("No file uploaded.");

		if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
			return TypedResults.BadRequest("Only CSV files are supported.");

		if (file.Length > 10 * 1024 * 1024)
			return TypedResults.BadRequest("File size exceeds the 10 MB limit.");

		if (string.IsNullOrWhiteSpace(account))
			return TypedResults.BadRequest("Account name is required.");

		return null;
	}
}
