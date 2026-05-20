using System.ComponentModel;
using BudgetTracker.Api.Features.Intelligence.Search;
using BudgetTracker.Api.Features.Transactions;
using Microsoft.EntityFrameworkCore.Storage.Json;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public record TransactionSearchResult
{
	public bool Success { get; init; }
	public int Count { get; init; }
	public string? Message { get; init; }
	public string Query { get; init; } = string.Empty;
	public List<TransactionSearchItem> Transactions { get; init; } = [];
}

public record TransactionSearchItem
{
	public Guid Id { get; init; }
	public string Date { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public decimal Amount { get; init; }
	public string? Category { get; init; }
	public string? Account { get; init; }
}

public class SearchTransactionsTool
{
	private readonly ILogger<SearchTransactionsTool> _logger;
	private readonly ISemanticSearchService _semanticSearchService;
	private readonly IAgentContext _agentContext;

	public SearchTransactionsTool(
		ILogger<SearchTransactionsTool> logger,
		ISemanticSearchService semanticSearchService,
		IAgentContext agentContext)
	{
		_logger = logger;
		_semanticSearchService = semanticSearchService;
		_agentContext = agentContext;
	}

	[Description("Search transactions using semantic search. Use this to find specific patterns, merchants, " +
				 "or transaction types. Examples: 'subscriptions', 'coffee shops', 'shopping', " +
				 "'dining'. Returns up to maxResults transactions with descriptions and amounts.")]
	public async Task<TransactionSearchResult> SearchTransactionsAsync(
		[Description("Natural language search query describing what transactions to find")]
		string query,
		[Description("Maximum number of results to return (default: 50, max: 100)")]
		int maxResults = 50)
	{
		_logger.LogInformation($"Search transactions called => Query:{query} | MaxResults: {maxResults}");

		List<Transaction> results = await _semanticSearchService.FindRelevantTransactionsAsync(query, _agentContext.UserId, maxResults);

		if (!results.Any())
		{
			return new TransactionSearchResult
			{
				Count = 0,
				Message = "No transactions found matching the query",
				Success = true,
				Transactions = [],
				Query = query
			};
		}

		var transactions = results
			.Select(x => new TransactionSearchItem
			{
				Id = x.Id,
				Description = x.Description,
				Date = x.Date.ToString("yyyy-MM-dd"),
				Category = x.Category,
				Amount = x.Amount,
				Account = x.Account
			})
			.ToList();

		return new TransactionSearchResult
		{
			Count = transactions.Count,
			Success = true,
			Transactions = transactions,
			Query = query
		};
	}
}