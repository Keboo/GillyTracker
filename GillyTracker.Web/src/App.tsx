import { Navigate, Route, Routes } from 'react-router-dom'
import ProtectedRoute from '@/components/ProtectedRoute'
import Login from '@/pages/Login'
import ReportSighting from '@/pages/ReportSighting'
import AdminSightings from '@/pages/AdminSightings'
import SightingDetail from '@/pages/SightingDetail'

function App() {
  return (
    <>
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
        <Route
          path="/admin/sightings/:id"
          element={(
            <ProtectedRoute requireAdmin>
              <SightingDetail />
            </ProtectedRoute>
          )}
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </>
  )
}

export default App
