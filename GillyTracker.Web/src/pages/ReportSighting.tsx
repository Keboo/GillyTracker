import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Icon, type Marker as LeafletMarker } from 'leaflet'
import { MapContainer, Marker, TileLayer, useMap, useMapEvents } from 'react-leaflet'
import { apiClient } from '@/services/apiClient'
import markerIconUrl from 'leaflet/dist/images/marker-icon.png'
import markerIconRetinaUrl from 'leaflet/dist/images/marker-icon-2x.png'
import markerShadowUrl from 'leaflet/dist/images/marker-shadow.png'

type SubmitState = 'idle' | 'saving' | 'saved' | 'failed'
type Coordinates = [number, number]

const defaultMapCenter: Coordinates = [39.8283, -98.5795]
const defaultMapZoom = 4
const detectedLocationZoom = 16
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

function MapRecenter({ center, zoom }: { center: Coordinates, zoom: number }) {
  const map = useMap()

  useEffect(() => {
    map.setView(center, zoom)
  }, [center, map, zoom])

  return null
}

function MapClickSelector({ onSelect }: { onSelect: (latitude: number, longitude: number) => void }) {
  useMapEvents({
    click: (event) => {
      onSelect(event.latlng.lat, event.latlng.lng)
    },
  })

  return null
}

export default function ReportSighting() {
  const [latitude, setLatitude] = useState<number | null>(null)
  const [longitude, setLongitude] = useState<number | null>(null)
  const [details, setDetails] = useState<string>('')
  const [mapCenter, setMapCenter] = useState<Coordinates>(defaultMapCenter)
  const [mapZoom, setMapZoom] = useState<number>(defaultMapZoom)
  const [locationMessage, setLocationMessage] = useState<string>(() =>
    navigator.geolocation
      ? 'Trying to read your location...'
      : 'Location services are unavailable on this device. Tap the map to set coordinates.',
  )
  const [submitState, setSubmitState] = useState<SubmitState>('idle')
  const [submitMessage, setSubmitMessage] = useState<string>('')
  const markerRef = useRef<LeafletMarker | null>(null)

  const markerPosition = useMemo<Coordinates | null>(() => {
    if (latitude === null || longitude === null) {
      return null
    }

    return [latitude, longitude]
  }, [latitude, longitude])

  const setSelectedCoordinates = useCallback((
    nextLatitude: number,
    nextLongitude: number,
    options?: { zoom?: number },
  ) => {
    setLatitude(nextLatitude)
    setLongitude(nextLongitude)
    setMapCenter([nextLatitude, nextLongitude])
    if (options?.zoom !== undefined) {
      setMapZoom(options.zoom)
    }
  }, [])

  useEffect(() => {
    if (!navigator.geolocation) {
      return
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        setSelectedCoordinates(position.coords.latitude, position.coords.longitude, { zoom: detectedLocationZoom })
        setLocationMessage('Location found. Drag the marker or tap the map to adjust before submitting.')
      },
      () => {
        setLocationMessage('Could not read location. Tap the map to set coordinates manually.')
      },
      { enableHighAccuracy: true, timeout: 10000 },
    )
  }, [setSelectedCoordinates])

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSubmitState('saving')
    setSubmitMessage('')

    try {
      if (latitude === null || longitude === null) {
        throw new Error('Please choose a location on the map before submitting.')
      }

      await apiClient.post('/api/sightings', {
        latitude,
        longitude,
        details,
      })

      setSubmitState('saved')
      setSubmitMessage('Thank you. Gilly\'s location report has been sent.')
      setDetails('')
    } catch (error) {
      setSubmitState('failed')
      setSubmitMessage(error instanceof Error ? error.message : 'Unable to submit report right now.')
    }
  }

  return (
    <main className="app-shell">
      <h1>Report Gilly&apos;s Location</h1>
      <p className="hint">{locationMessage}</p>
      <form onSubmit={submit} className="report-form">
        <section className="map-section" aria-label="Location map section">
          <p className="map-hint">Tap anywhere on the map to pick the sighting location.</p>
          <MapContainer className="location-map" center={mapCenter} zoom={mapZoom} scrollWheelZoom>
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <MapRecenter center={mapCenter} zoom={mapZoom} />
            <MapClickSelector
              onSelect={(nextLatitude, nextLongitude) => {
                setSelectedCoordinates(nextLatitude, nextLongitude)
              }}
            />
            {markerPosition && (
              <Marker
                draggable
                eventHandlers={{
                  dragend: () => {
                    const marker = markerRef.current
                    if (!marker) {
                      return
                    }

                    const point = marker.getLatLng()
                    setSelectedCoordinates(point.lat, point.lng)
                  },
                }}
                icon={markerIcon}
                position={markerPosition}
                ref={markerRef}
              />
            )}
          </MapContainer>
          <div className="coordinate-readout" aria-live="polite">
            <span className="coordinate-pill">
              Lat:&nbsp;
              {latitude === null ? 'Not set' : latitude.toFixed(7)}
            </span>
            <span className="coordinate-pill">
              Long:&nbsp;
              {longitude === null ? 'Not set' : longitude.toFixed(7)}
            </span>
          </div>
        </section>
        <label>
          Contact details or notes
          <textarea
            value={details}
            onChange={(event) => setDetails(event.target.value)}
            rows={5}
            maxLength={2000}
            placeholder="How can I reach you? Where is Gilly now?"
          />
        </label>
        <button type="submit" disabled={submitState === 'saving'}>
          {submitState === 'saving' ? 'Sending...' : 'Send report'}
        </button>
        {submitMessage && <p className={submitState === 'failed' ? 'error' : 'success'}>{submitMessage}</p>}
      </form>
    </main>
  )
}
