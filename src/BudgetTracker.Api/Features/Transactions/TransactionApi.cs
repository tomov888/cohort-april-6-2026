using BudgetTracker.Api.Features.Transactions.Import;
using BudgetTracker.Api.Features.Transactions.List;

namespace BudgetTracker.Api.Features.Transactions;

public static class TransactionApi
{
	public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder routes)
	{
		var transactionsGroup = routes.MapGroup("/transactions")
			.WithTags("Transactions")
			.RequireAuthorization();

		transactionsGroup.MapTransactionListEndpoint();
		transactionsGroup.MapTransactionImportEndpoint();

		return routes;
	}
}
