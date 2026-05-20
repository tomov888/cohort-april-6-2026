import Header from '../shared/components/layout/Header';
import { intelligenceApi, QueryAssistant, RecommendationsCard } from '../features/intelligence';
import { useState, useEffect } from 'react';
import type { ProactiveRecommendation } from '../features/intelligence';
import { analyticsApi, InsightsCard, type BudgetInsights } from '../features/analytics';

export async function loader() {
	return {};
}

export default function Dashboard() {
	const [insights, setInsights] = useState<BudgetInsights | null>(null);
	const [isLoadingInsights, setIsLoadingInsights] = useState(false);
	const [insightsError, setInsightsError] = useState(false);
	const [recommendations, setRecommendations] = useState<ProactiveRecommendation[]>([]);

	useEffect(() => {
		intelligenceApi.agentRecommendations().then(setRecommendations).catch(() => []);
	}, []);

	const generateInsights = async () => {
		setIsLoadingInsights(true);
		setInsightsError(false);
		try {
			const data = await analyticsApi.getInsights();
			setInsights(data);
		} catch {
			setInsightsError(true);
		} finally {
			setIsLoadingInsights(false);
		}
	};

	return (
		<div className="px-4 py-6 sm:px-0">
			<Header
				title="Dashboard"
				subtitle="Analytics insights and query assistant"
			/>

			{recommendations.length > 0 && (
				<div className="mb-6">
					<RecommendationsCard recommendations={recommendations} />
				</div>
			)}

			<div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
				<InsightsCard
					insights={insights}
					isLoading={isLoadingInsights}
					hasError={insightsError}
					onGenerate={generateInsights}
				/>
				<QueryAssistant />
			</div>
		</div>
	);
}