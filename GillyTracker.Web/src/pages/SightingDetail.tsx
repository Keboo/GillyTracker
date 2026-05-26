import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Icon } from 'leaflet'
import { MapContainer, Marker, TileLayer } from 'react-leaflet'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import { IconButton } from '@mui/material'
import { apiClient } from '@/services/apiClient'
import type { SightingResponse } from '@/types'
import markerIconUrl from 'leaflet/dist/images/marker-icon.png'
import markerIconRetinaUrl from 'leaflet/dist/images/marker-icon-2x.png'
import markerShadowUrl from 'leaflet/dist/images/marker-shadow.png'

const markerIcon = new Icon({
  iconUrl: markerIconUrl,
  iconRetinaUrl: markerIconRetinaUrl,
  shadowUrl: markerShadowUrl,
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  popupAnchor: [1, -34],
  tooltipAnchor: [16, -28],
  shadowSize: [41, 41],
})

export default function SightingDetail() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [sighting, setSighting] = useState<SightingResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [errorMessage, setErrorMessage] = useState<string>('')

  useEffect(() => {
    if (!id) return

    const loadSighting = async () => {
      setLoading(true)
      setErrorMessage('')
      try {
        const data = await apiClient.get<SightingResponse>(`/api/sightings/${id}`)
        setSighting(data)
      } catch (error) {
        setErrorMessage(error instanceof Error ? error.message : 'Unable to load sighting.')
      } finally {
        setLoading(false)
      }
    }

    void loadSighting()
  }, [id])

  return (
    <main className="app-shell">
      <div className="detail-header">
        <IconButton
          aria-label="Back to sightings"
          onClick={() => navigate('/admin/sightings')}
          size="small"
          title="Back to sightings"
        >
          <ArrowBackIcon />
        </IconButton>
        <h1>Sighting Details</h1>
      </div>

      {loading && <p>Loading sighting...</p>}
      {errorMessage && <p className="error">{errorMessage}</p>}

      {!loading && !errorMessage && sighting && (
        <div className="sighting-detail">
          <dl className="detail-fields">
            <dt>Reported</dt>
            <dd>{new Date(sighting.createdDate).toLocaleString()}</dd>
            <dt>Coordinates</dt>
            <dd>
              <span className="coordinate-pill">Lat:&nbsp;{sighting.latitude.toFixed(7)}</span>
              {' '}
              <span className="coordinate-pill">Long:&nbsp;{sighting.longitude.toFixed(7)}</span>
            </dd>
            {sighting.details && (
              <>
                <dt>Details</dt>
                <dd>{sighting.details}</dd>
              </>
            )}
          </dl>

          <MapContainer
            className="location-map detail-map"
            center={[Number(sighting.latitude), Number(sighting.longitude)]}
            zoom={14}
            scrollWheelZoom={false}
          >
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <Marker
              icon={markerIcon}
              position={[Number(sighting.latitude), Number(sighting.longitude)]}
            />
          </MapContainer>
        </div>
      )}

      {!loading && !errorMessage && !sighting && (
        <p>Sighting not found.</p>
      )}
    </main>
  )
}
