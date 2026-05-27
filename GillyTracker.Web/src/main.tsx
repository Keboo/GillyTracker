import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { registerSW } from 'virtual:pwa-register'
import App from './App.tsx'
import { AuthProvider } from './contexts/AuthContext.tsx'
import { ThemeProvider } from './contexts/ThemeContext.tsx'
import { initTelemetry } from './services/telemetry'
import 'leaflet/dist/leaflet.css'
import './index.css'

initTelemetry()

const updateSW = registerSW({
  immediate: true,
  onRegisteredSW: (_swUrl, registration) => {
    if (!registration) {
      return
    }

    setInterval(() => {
      void registration.update()
    }, 60 * 1000)
  },
  onNeedRefresh: () => {
    void updateSW(true)
  },
})

let hasReloadedForServiceWorkerUpdate = false

if ('serviceWorker' in navigator) {
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (hasReloadedForServiceWorkerUpdate) {
      return
    }

    hasReloadedForServiceWorkerUpdate = true
    window.location.reload()
  })
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <ThemeProvider>
        <AuthProvider>
          <App />
        </AuthProvider>
      </ThemeProvider>
    </BrowserRouter>
  </StrictMode>,
)
