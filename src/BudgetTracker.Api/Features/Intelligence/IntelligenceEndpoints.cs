using BudgetTracker.Api.Features.Intelligence.Recommendations;
using BudgetTracker.Api.Features.Intelligence.Search;

namespace BudgetTracker.Api.Features.Intelligence;

public static class IntelligenceEndpoints
{
	public static IEndpointRouteBuilder MapIntelligenceEndpoints(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapQueryEndpoints();
		endpoints.MapRecommendationEndpoints();
		return endpoints;
	}
}
