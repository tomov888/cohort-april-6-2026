import { apiClient } from '../../api';
import type {
	TransactionListDto,
	GetTransactionsParams,
	ImportTransactionsParams,
	ImportResult
} from './types';

export const transactionsApi = {
	async getTransactions(params: GetTransactionsParams = {}): Promise<TransactionListDto> {
		const { page = 1, pageSize = 10 } = params;
		const response = await apiClient.get<TransactionListDto>('/transactions', {
			params: { page, pageSize }
		});
		return response.data;
	},

	async importTransactions(params: ImportTransactionsParams): Promise<ImportResult> {
		const response = await apiClient.post<ImportResult>('/transactions/import', params.formData, {
			headers: { 'Content-Type': 'multipart/form-data' },
			onUploadProgress: params.onUploadProgress
		});
		return response.data;
	}
};
