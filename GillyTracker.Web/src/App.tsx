import { Link, Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'
import ProtectedRoute from '@/components/ProtectedRoute'
import Login from '@/pages/Login'
import ReportSighting from '@/pages/ReportSighting'
import AdminSightings from '@/pages/AdminSightings'

function App() {
  const { user, loading, beginMicrosoftLogin, logout } = useAuth()

  return (
    <>
      <header className="site-header">
        <div className="site-header-content">
          <nav>
            <Link to="/">Report sighting</Link>
            <Link to="/admin/sightings">View sightings</Link>
          </nav>
          <div className="auth-actions">
            {loading && <span>Checking sign-in...</span>}
            {!loading && user?.isAuthenticated && (
              <>
                <span className="signed-in-as">{user.email}</span>
                <button
                  type="button"
                  onClick={() => {
                    void logout()
                  }}
                >
                  Sign out
                </button>
              </>
            )}
            {!loading && !user?.isAuthenticated && (
              <button type="button" onClick={() => beginMicrosoftLogin('/admin/sightings')}>
                Admin sign in
              </button>
            )}
          </div>
        </div>
      </header>

      <Routes>
        <Route path="/" element={<ReportSighting />} />
        <Route path="/login" element={<Login />} />
        <Route
          path="/admin/sightings"
          element={(
            <ProtectedRoute requireAdmin>
              <AdminSightings />
            </ProtectedRoute>
          )}
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  )
}

export default App
