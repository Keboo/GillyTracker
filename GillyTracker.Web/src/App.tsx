import LockOutlinedIcon from '@mui/icons-material/LockOutlined'
import LogoutOutlinedIcon from '@mui/icons-material/LogoutOutlined'
import CircularProgress from '@mui/material/CircularProgress'
import IconButton from '@mui/material/IconButton'
import Tooltip from '@mui/material/Tooltip'
import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'
import ProtectedRoute from '@/components/ProtectedRoute'
import Login from '@/pages/Login'
import ReportSighting from '@/pages/ReportSighting'
import AdminSightings from '@/pages/AdminSightings'

function App() {
  const { user, loading, beginMicrosoftLogin, logout } = useAuth()

  return (
    <>
      <div className="auth-fab">
        {loading ? (
          <span className="auth-fab-loading" aria-label="Checking sign-in status">
            <CircularProgress color="inherit" size={20} />
          </span>
        ) : (
          <Tooltip title={user?.isAuthenticated ? 'Sign out' : 'Admin sign in'}>
            <IconButton
              aria-label={user?.isAuthenticated ? 'Sign out' : 'Admin sign in'}
              className="auth-fab-button"
              onClick={() => {
                if (user?.isAuthenticated) {
                  void logout()
                  return
                }

                beginMicrosoftLogin('/admin/sightings')
              }}
              size="large"
            >
              {user?.isAuthenticated ? <LogoutOutlinedIcon /> : <LockOutlinedIcon />}
            </IconButton>
          </Tooltip>
        )}
      </div>

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
