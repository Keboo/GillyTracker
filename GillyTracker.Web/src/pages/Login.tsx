import { useMemo } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'

export default function Login() {
  const { user, beginMicrosoftLogin } = useAuth()
  const location = useLocation()

  const errorMessage = useMemo(() => {
    const search = new URLSearchParams(location.search)
    const error = search.get('error')

    if (error === 'auth_failed') {
      return 'Microsoft sign-in was not completed.'
    }

    return ''
  }, [location.search])

  if (user?.isAuthenticated && user.isAdmin) {
    return <Navigate to="/admin/sightings" replace />
  }

  return (
    <main className="app-shell">
      <h1>Admin Sign In</h1>
      <p className="hint">Only users in the PetTrackerAdmins Entra group can access the sightings list.</p>
      {errorMessage && <p className="error">{errorMessage}</p>}
      <button
        type="button"
        onClick={() => beginMicrosoftLogin('/admin/sightings')}
        className="microsoft-login-button"
      >
        Continue with Microsoft
      </button>
    </main>
  )
}
