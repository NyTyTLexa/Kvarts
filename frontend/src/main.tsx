import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { AuthProvider } from 'react-oidc-context'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { userManager } from './auth/oidc'
import App from './App'
import { bootTheme } from './ui/theme'
import './styles/global.css'

bootTheme()

// StrictMode намеренно не используем: его двойной вызов эффектов конфликтует с
// обработкой OIDC-callback (code/state обрабатывается дважды).
const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
})

createRoot(document.getElementById('root')!).render(
  <AuthProvider
    userManager={userManager}
    onSigninCallback={() => {
      window.history.replaceState({}, document.title, window.location.pathname)
    }}
  >
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </AuthProvider>,
)
