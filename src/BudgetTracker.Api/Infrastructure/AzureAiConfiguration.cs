using System;

namespace BudgetTracker.Api.Infrastructure;

public record AzureAiConfiguration
{
	public const string SectionName = "AzureAI";

	public string Endpoint { get; set; } = string.Empty;
	public string ApiKey { get; set; } = string.Empty;
	public string DeploymentName { get; set; } = string.Empty;
	public string EmbeddingDeploymentName { get; set; } = string.Empty;
}
