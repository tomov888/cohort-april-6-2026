using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using BudgetTracker.Api.Features.Transactions.Import;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public class CsvImporter
{
	public async Task<(ImportResult Result, List<Transaction> Transactions)> ParseCsvAsync(
		Stream csvStream, string sourceFileName, string userId, string account)
	{
		var importedAt = DateTime.UtcNow;
		var result = new ImportResult
		{
			SourceFile = sourceFileName,
			ImportedAt = importedAt,
			ImportSessionHash = ComputeSessionHash(sourceFileName, importedAt),
		};

		var transactions = new List<Transaction>();

		try
		{
			using var reader = new StreamReader(csvStream, Encoding.UTF8);
			var firstLine = await reader.ReadLineAsync() ?? string.Empty;
			var delimiter = DetectDelimiter(firstLine);

			csvStream.Position = 0;
			using var csvReader = new StreamReader(csvStream, Encoding.UTF8);
			using var csv = new CsvReader(csvReader, new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				HasHeaderRecord = true,
				MissingFieldFound = null,
				BadDataFound = null,
				TrimOptions = TrimOptions.Trim,
				Delimiter = delimiter,
			});

			var rowNumber = 0;

			await foreach (var record in csv.GetRecordsAsync<dynamic>())
			{
				rowNumber++;
				result.TotalRows++;

				try
				{
					var transaction = ParseRow(record, userId, account);
					transactions.Add(transaction);
					result.ImportedCount++;
				}
				catch (Exception ex)
				{
					result.FailedCount++;
					result.Errors.Add($"Row {rowNumber}: {ex.Message}");
				}
			}

			return (result, transactions);
		}
		catch (Exception ex)
		{
			result.Errors.Add($"CSV parsing failed: {ex.Message}");
			return (result, []);
		}
	}

	private static Transaction ParseRow(dynamic record, string userId, string account)
	{
		var row = (IDictionary<string, object>)record;

		var description = GetColumn(row, "Description", "Descrição", "Memo", "Details")
			?? throw new InvalidOperationException("Description is required.");

		var dateStr = GetColumn(row, "Date", "Transaction Date", "Data Lanc.", "Posting Date")
			?? throw new InvalidOperationException("Date is required.");

		var amountStr = GetColumn(row, "Amount", "Valor", "Transaction Amount", "Debit", "Credit")
			?? throw new InvalidOperationException("Amount is required.");

		var balanceStr = GetColumn(row, "Balance", "Running Balance", "Saldo", "Account Balance");
		var category = GetColumn(row, "Category", "Type", "Transaction Type");

		if (!TryParseDate(dateStr, out var date))
			throw new InvalidOperationException($"Unrecognised date format: '{dateStr}'.");

		if (!TryParseDecimal(amountStr, out var amount))
			throw new InvalidOperationException($"Unrecognised amount format: '{amountStr}'.");

		decimal? balance = null;
		if (!string.IsNullOrWhiteSpace(balanceStr) && TryParseDecimal(balanceStr, out var parsedBalance))
			balance = parsedBalance;

		return new Transaction
		{
			Id = Guid.NewGuid(),
			Date = date,
			Description = description.Trim(),
			Amount = amount,
			Balance = balance,
			Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
			ImportedAt = DateTime.UtcNow,
			Account = account,
			UserId = userId,
		};
	}

	private static string? GetColumn(IDictionary<string, object> row, params string[] candidates)
	{
		foreach (var name in candidates)
		{
			if (row.TryGetValue(name, out var value) && value is not null)
			{
				var str = value.ToString()?.Trim();
				if (!string.IsNullOrEmpty(str))
					return str;
			}
		}

		return null;
	}

	private static bool TryParseDate(string value, out DateTime date)
	{
		string[] formats = ["yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy"];
		return DateTime.TryParseExact(value.Trim(), formats,
			CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
			out date);
	}

	private static bool TryParseDecimal(string value, out decimal result)
	{
		var clean = value.Trim()
			.Replace("$", "").Replace("€", "").Replace("£", "").Replace("¥", "")
			.Trim();

		if (decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out result))
			return true;

		// Portuguese format: "4 888,00" — remove spaces, swap comma for dot
		var ptClean = clean.Replace(" ", "").Replace(",", ".");
		return decimal.TryParse(ptClean, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
	}

	private static string DetectDelimiter(string headerLine)
		=> headerLine.Count(c => c == ';') > headerLine.Count(c => c == ',') ? ";" : ",";

	private static string ComputeSessionHash(string fileName, DateTime importedAt)
	{
		var input = $"{fileName}:{importedAt:O}";
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return Convert.ToHexString(hash)[..16].ToLowerInvariant();
	}
}
