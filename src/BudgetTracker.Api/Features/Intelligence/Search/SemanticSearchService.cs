using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public interface ISemanticSearchService
{
	Task<List<Transaction>> FindRelevantTransactionsAsync(string queryText, string userId, int maxResults = 50);
}

public class SemanticSearchService:ISemanticSearchService
{
	private readonly ILogger<SemanticSearchService> _logger;
	private readonly IAzureEmbeddingService _embedding;
	private readonly BudgetTrackerContext  _context;

	public SemanticSearchService(
		ILogger<SemanticSearchService> logger, 
		IAzureEmbeddingService embedding, 
		BudgetTrackerContext context)
	{
		_logger = logger;
		_embedding = embedding;
		_context = context;
	}


	public async Task<List<Transaction>> FindRelevantTransactionsAsync(string queryText, string userId, int maxResults = 50)
	{
		if (string.IsNullOrWhiteSpace(queryText) || string.IsNullOrWhiteSpace(userId))
		{
			return new List<Transaction>();
		}

		try
		{
			Vector queryEmbedding = await _embedding.GenerateEmbeddingAsync(queryText);
			string vectorString = queryEmbedding.ToString();
			List<Transaction> similarTransactions = await _context.Transactions
				.FromSqlRaw(
				@"
                    SELECT *
                    FROM ""Transactions""
                    WHERE ""Embedding"" IS NOT NULL
                    AND ""UserId"" = {0}
                    ORDER BY cosine_distance(""Embedding"", {1}::vector) ASC
                    LIMIT {2}
                "
				, userId, vectorString, maxResults)
				.ToListAsync();			

			_logger.LogInformation("Found {Count} relevant transactions for query: {Query}", similarTransactions.Count, queryText[..Math.Min(queryText.Length, 50)]);
			return similarTransactions;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to find relevant transactions for query: {Query}", queryText);
			return new List<Transaction>();
		}
	}
}