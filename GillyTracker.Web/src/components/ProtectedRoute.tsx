import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'
import { CircularProgress, Box } from '@mui/material'

interface ProtectedRouteProps {
  children: ReactNode
  requireAdmin?: boolean
}

export default function ProtectedRoute({ children, requireAdmin = false }: ProtectedRouteProps) {
  const { user, loading } = useAuth()

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '50vh' }}>
        <CircularProgress aria-label="Loading" />
      </Box>
    )
  }

  if (!user?.isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (requireAdmin && !user.isAdmin) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}
