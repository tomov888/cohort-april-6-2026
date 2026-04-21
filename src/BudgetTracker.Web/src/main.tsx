import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { Login, Register } from './features/auth'
import ErrorBoundary from './shared/components/ErrorBoundary'
import ToastContainer from './shared/components/ToastContainer'
import { ToastProvider } from './shared/contexts/ToastContext'
import './index.css'
import { authLoader } from './routes/authLoader'
import Dashboard, { loader as dashboardLoader } from './routes/dashboard'
import Import from './routes/import'
import Root from './routes/root'
import Transactions, { transactionsLoader as transactionsLoader } from './routes/transactions'
import ImportPage from './pages/ImportPage'

const router = createBrowserRouter([
	{
		path: '/login',
		element: <Login />,
	},
	{
		path: '/register',
		element: <Register />,
	},
	{
		path: '/',
		element: <Root />,
		errorElement: <ErrorBoundary><div /></ErrorBoundary>,
		loader: authLoader,
		children: [
			{
				index: true,
				element: <Dashboard />,
				loader: dashboardLoader,
			},
			{
				path: 'dashboard',
				element: <Dashboard />,
				loader: dashboardLoader,
			},
			{
				path: 'transactions',
				element: <Transactions />,
				loader: transactionsLoader,
			},
			{
				path: '/import',
				element: <ImportPage />
			}
		],
	},
], {
	future: {
		v7_partialHydration: true,
	},
})

createRoot(document.getElementById('root')!).render(
	<StrictMode>
		<ToastProvider>
			<RouterProvider router={router} />
			<ToastContainer />
		</ToastProvider>
	</StrictMode>,
)