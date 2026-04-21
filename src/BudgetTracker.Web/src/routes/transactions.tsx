import { type LoaderFunctionArgs } from 'react-router-dom';
import Header from '../shared/components/layout/Header';
import { transactionsApi } from '../features/transactions/transactions-api';
import TransactionList from '../features/transactions/components/TransactionList';

// Data loader for transactions page
export async function transactionsLoader({ request }: LoaderFunctionArgs) {
	const url = new URL(request.url);
	const page = parseInt(url.searchParams.get('page') || '1', 10);
	const pageSize = parseInt(url.searchParams.get('pageSize') || '10', 10);

	return await transactionsApi.getTransactions({ page, pageSize });
}

export default function Transactions() {
	return (
		<div className="px-4 py-6 sm:px-0">
			<Header
				title="Transactions"
				subtitle="Manage your transactions"
			/>

			<div className="mt-6">
				<TransactionList />
			</div>
		</div>
	);
}