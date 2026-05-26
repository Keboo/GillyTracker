import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import DeleteIcon from '@mui/icons-material/Delete'
import { IconButton } from '@mui/material'
import { apiClient } from '@/services/apiClient'
import type { SightingResponse } from '@/types'

export default function AdminSightings() {
  const navigate = useNavigate()
  const [sightings, setSightings] = useState<SightingResponse[]>([])
  const [loading, setLoading] = useState(true)
  const [errorMessage, setErrorMessage] = useState<string>('')
  const [deletingId, setDeletingId] = useState<string | null>(null)

  useEffect(() => {
    const loadSightings = async () => {
      setLoading(true)
      setErrorMessage('')

      try {
        const data = await apiClient.get<SightingResponse[]>('/api/sightings')
        setSightings(data)
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : 'Unable to load sightings.')
      } finally {
        setLoading(false)
      }
    }

    void loadSightings()
  }, [])

  const orderedSightings = useMemo(
    () =>
      [...sightings].sort(
        (a, b) => new Date(b.createdDate).getTime() - new Date(a.createdDate).getTime(),
      ),
    [sightings],
  )

  const deleteSighting = async (id: string) => {
    if (!window.confirm('Are you sure you want to delete this sighting? This cannot be undone.')) {
      return
    }
    setDeletingId(id)
    try {
      await apiClient.delete(`/api/sightings/${id}`)
      setSightings((current) => current.filter((sighting) => sighting.id !== id))
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : 'Unable to delete sighting.')
    } finally {
      setDeletingId(null)
    }
  }

  return (
    <main className="app-shell">
      <h1>Posted Sightings</h1>
      <p className="hint">Latest sightings appear first.</p>

      {loading && <p>Loading sightings...</p>}
      {errorMessage && <p className="error">{errorMessage}</p>}

      {!loading && !errorMessage && orderedSightings.length === 0 && <p>No sightings have been posted yet.</p>}

      {!loading && orderedSightings.length > 0 && (
        <div className="sightings-table-wrapper">
          <table className="sightings-table">
            <thead>
              <tr>
                <th>Reported</th>
                <th>Details</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {orderedSightings.map((sighting) => (
                <tr
                  key={sighting.id}
                  className="sightings-table-row"
                  onClick={() => navigate(`/admin/sightings/${sighting.id}`)}
                >
                  <td>{new Date(sighting.createdDate).toLocaleString()}</td>
                  <td>{sighting.details || '-'}</td>
                  <td className="sightings-table-actions">
                    <IconButton
                      aria-label="Delete sighting"
                      className="delete-icon-button"
                      disabled={deletingId === sighting.id}
                      size="small"
                      title="Delete sighting"
                      onClick={(e) => {
                        e.stopPropagation()
                        void deleteSighting(sighting.id)
                      }}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </main>
  )
}
