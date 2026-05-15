using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using BudgetTracker.Api.Features.Intelligence.Search;
using BudgetTracker.Api.Infrastructure;
using Microsoft.Extensions.AI;
using BudgetTracker.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Import.Enhancement;

public interface ITransactionEnhancer
{
	Task<List<EnhancedTransactionDescription>> EnhanceDescriptionsAsync(
		List<string> descriptions,
		string account,
		string userId,
		string? currentImportSessionHash = null);
}

public class TransactionEnhancer : ITransactionEnhancer
{
	private readonly IChatClient _chatClient;
	private readonly ILogger<TransactionEnhancer> _logger;
	private readonly IAzureEmbeddingService _embeddingService;
	private readonly BudgetTrackerContext _context;

	private const int DefaultContextLimit = 25; // Number of context transactions to retrieve
	private const int ContextWindowDays = 365; // Time window for context retrieval

	public TransactionEnhancer(
		IChatClient chatClient,
		ILogger<TransactionEnhancer> logger,
		IAzureEmbeddingService embeddingService,
		BudgetTrackerContext context)
	{
		_chatClient = chatClient;
		_logger = logger;
		_embeddingService = embeddingService;
		_context = context;
	}

	public async Task<List<EnhancedTransactionDescription>> EnhanceDescriptionsAsync(
		List<string> descriptions,
		string account,
		string userId,
		string currentImportSessionHash) // Required parameter
	{
		if (!descriptions.Any())
			return new List<EnhancedTransactionDescription>();

		var stopwatch = Stopwatch.StartNew();

		try
		{
			// Get semantically similar transactions for context
			var contextTransactions = await GetSemanticContextTransactionsAsync(descriptions, userId, account,
				DefaultContextLimit, currentImportSessionHash);

			_logger.LogInformation("Retrieved {ContextCount} context transactions for account {Account}",
				contextTransactions.Count, account);

			// Always create enhanced system prompt with available context
			var systemPrompt = CreateEnhancedSystemPrompt(contextTransactions);
			var userPrompt = CreateUserPrompt(descriptions);

			// Use Microsoft.Extensions.AI IChatClient
			var response = await _chatClient.GetResponseAsync([
				new ChatMessage(ChatRole.System, systemPrompt),
				new ChatMessage(ChatRole.User, userPrompt)
			]);

			var content = response.Text ?? string.Empty;
			var results = ParseEnhancedDescriptions(content, descriptions);

			_logger.LogInformation("AI processing completed in {ProcessingTime}ms", stopwatch.ElapsedMilliseconds);

			return results;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to enhance transaction descriptions");
			return descriptions.Select(d => new EnhancedTransactionDescription
			{
				OriginalDescription = d,
				EnhancedDescription = d,
				ConfidenceScore = 0.0
			}).ToList();
		}
	}

	private async Task<List<Transaction>> GetSemanticContextTransactionsAsync(
		List<string> descriptions,
		string userId,
		string account,
		int limit,
		string excludeImportSessionHash)
	{
		try
		{
			// Combine all descriptions into a single query for embedding
			var combinedQuery = string.Join(" ", descriptions.Take(5)); // Limit to avoid token overflow

			// Generate embedding for the combined descriptions
			var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(combinedQuery);
			var vectorString = queryEmbedding.ToString();

			var cutoffDate = DateTime.UtcNow.AddDays(-ContextWindowDays);

			// Build the base query conditions
			var conditions = new List<string>
			{
				"\"Embedding\" IS NOT NULL",
				"\"UserId\" = {0}",
				"\"Account\" = {1}",
				"\"ImportedAt\" >= {2}",
				"\"Category\" IS NOT NULL AND \"Category\" != ''",
				"\"ImportSessionHash\" != {3}",
			};

			var parameters = new List<object>
				{ userId, account, cutoffDate, excludeImportSessionHash, vectorString, limit };

			var whereClause = string.Join(" AND ", conditions);

			// Use semantic similarity with cosine distance, but also factor in recency
			var similarTransactions = await _context.Transactions
				.FromSqlRaw(
					$@"
		                SELECT *
		                FROM ""Transactions""
		                WHERE {whereClause}
		                AND cosine_distance(""Embedding"", {{4}}::vector) < 0.6
		                ORDER BY cosine_distance(""Embedding"", {{4}}::vector) ASC, ""Date"" DESC
		            	LIMIT {{5}}
		            ",
					parameters.ToArray())
				.ToListAsync();

			_logger.LogInformation("Found {Count} semantically similar context transactions for enhancement",
				similarTransactions.Count);

			return similarTransactions;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to get semantic context, falling back to empty list");

			// Fallback to empty list - better to proceed without context than fail
			return new List<Transaction>();
		}
	}

	private string CreateEnhancedSystemPrompt(List<Transaction> contextTransactions)
	{
		var basePrompt = """
		                 You are a transaction categorization assistant. Your job is to clean up messy bank transaction descriptions and make them more readable and meaningful for users.

		                 Guidelines:
		                 1. Transform cryptic merchant codes and bank jargon into clear, readable descriptions
		                 2. Remove unnecessary reference numbers, codes, and technical identifiers
		                 3. Identify the actual merchant or service provider
		                 4. Suggest appropriate spending categories when possible
		                 5. Maintain accuracy - don't invent information not present in the original
		                 """;

		if (contextTransactions.Any())
		{
			var contextSection = "\n\nSIMILAR TRANSACTIONS for this account:\n";
			contextSection += string.Join("\n", contextTransactions.Select(t =>
				$"- \"{t.Description}\" → Amount: {t.Amount:C} → Category: \"{t.Category}\"").Distinct());

			contextSection +=
				"\n\nThese transactions were selected based on semantic similarity to the new transactions being processed.";
			contextSection +=
				"\nUse these patterns to inform your categorization decisions, paying special attention to:";
			contextSection += "\n- Similar merchant names or transaction types";
			contextSection += "\n- Comparable amount ranges for similar categories";
			contextSection += "\n- Established categorization patterns for this user";

			basePrompt += contextSection;
		}

		basePrompt += """

		              Examples:
		              - "AMZN MKTP US*123456789" → "Amazon Marketplace Purchase"
		              - "STARBUCKS COFFEE #1234" → "Starbucks Coffee"
		              - "SHELL OIL #4567" → "Shell Gas Station"
		              - "DD VODAFONE PORTU 222111000 PT00110011" → "Vodafone Portugal - Direct Debit"
		              - "COMPRA 0000 TEMU.COM DUBLIN" → "Temu Online Purchase"
		              - "TRF MB WAY P/ Manuel Silva" → "MB WAY Transfer to Manuel Silva"

		              Respond with a JSON array where each object has:
		              - "originalDescription": the input description
		              - "enhancedDescription": the cleaned description
		              - "suggestedCategory": optional category (e.g., "Groceries", "Entertainment", "Transportation", "Utilities", "Shopping", "Food & Drink", "Gas & Fuel", "Transfer")
		              - "confidenceScore": number between 0-1 indicating confidence in the enhancement

		              Be conservative with confidence scores - only use high scores (>0.8) when you're very certain about the merchant identification.
		              """;

		return basePrompt;
	}

	private static string CreateSystemPrompt()
	{
		return """
		       You are a transaction enhancement and categorization assistant. Your job is to clean up messy bank transaction descriptions and suggest appropriate spending categories.

		       Guidelines:
		       1. Transform cryptic merchant codes and bank jargon into clear, readable descriptions
		       2. Remove unnecessary reference numbers, codes, and technical identifiers
		       3. Identify the actual merchant or service provider
		       4. Suggest appropriate spending categories based on the merchant type and transaction purpose
		       5. Maintain accuracy - don't invent information not present in the original

		       Examples:
		       - "AMZN MKTP US*123456789" → "Amazon Marketplace Purchase" (Category: Shopping)
		       - "STARBUCKS COFFEE #1234" → "Starbucks Coffee" (Category: Food & Drink)
		       - "SHELL OIL #4567" → "Shell Gas Station" (Category: Gas & Fuel)
		       - "DD VODAFONE PORTU 222111000" → "Vodafone Portugal - Direct Debit" (Category: Utilities)
		       - "COMPRA 0000 TEMU.COM DUBLIN" → "Temu Online Purchase" (Category: Shopping)
		       - "TRF MB WAY P/ Manuel Silva" → "MB WAY Transfer to Manuel Silva" (Category: Transfer)

		       Common categories to use:
		       - Shopping, Groceries, Food & Drink, Entertainment, Gas & Fuel
		       - Utilities, Transportation, Healthcare, Transfer, Cash & ATM
		       - Technology, Subscriptions, Travel, Education, Other

		       Respond with a JSON array where each object has:
		       - "originalDescription": the input description
		       - "enhancedDescription": the cleaned description
		       - "suggestedCategory": appropriate category from the list above
		       - "confidenceScore": number between 0-1 indicating confidence in both enhancement and categorization

		       Be conservative with confidence scores - only use high scores (>0.8) when you're very certain about the merchant identification and category.
		       """;
	}

	private static string CreateUserPrompt(List<string> descriptions)
	{
		var descriptionsJson = JsonSerializer.Serialize(descriptions);
		return $"Please enhance these transaction descriptions:\n{descriptionsJson}";
	}

	private List<EnhancedTransactionDescription> ParseEnhancedDescriptions(
		string content,
		List<string> originalDescriptions)
	{
		try
		{
			var jsonContent = content.ExtractJsonFromCodeBlock();
			var enhancedDescriptions = JsonSerializer.Deserialize<List<EnhancedTransactionDescription>>(
				jsonContent,
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			if (enhancedDescriptions?.Count == originalDescriptions.Count)
			{
				return enhancedDescriptions;
			}
		}
		catch (JsonException ex)
		{
			_logger.LogWarning(ex, "Failed to parse AI response as JSON: {Content}", content);
		}
		catch (FormatException ex)
		{
			_logger.LogWarning(ex, "Failed to extract JSON from AI response");
		}

		_logger.LogWarning("AI response format was invalid, returning original descriptions");
		return CreateFallbackResponse(originalDescriptions);
	}

	private static List<EnhancedTransactionDescription> CreateFallbackResponse(List<string> descriptions)
	{
		return descriptions.Select(d => new EnhancedTransactionDescription
		{
			OriginalDescription = d,
			EnhancedDescription = d,
			SuggestedCategory = null,
			ConfidenceScore = 0.0
		}).ToList();
	}
}

public class EnhancedTransactionDescription
{
	public string OriginalDescription { get; set; } = string.Empty;
	public string EnhancedDescription { get; set; } = string.Empty;
	public string? SuggestedCategory { get; set; }
	public double ConfidenceScore { get; set; }
}