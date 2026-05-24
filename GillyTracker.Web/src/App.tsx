import type { LatLngTuple } from 'leaflet'
import { FormEvent, useEffect, useMemo, useState } from 'react'
import { CircleMarker, MapContainer, TileLayer, useMap, useMapEvents } from 'react-leaflet'
import { apiClient } from './services/apiClient'

type SubmitState = 'idle' | 'saving' | 'saved' | 'failed'
type Coordinates = { latitude: number; longitude: number }

const defaultMapCenter: LatLngTuple = [0, 0]
const defaultMapZoom = 2
const selectedMapZoom = 16

const isLatitudeInRange = (value: number) => value >= -90 && value <= 90
const isLongitudeInRange = (value: number) => value >= -180 && value <= 180

const parseCoordinates = (latitudeValue: string, longitudeValue: string): Coordinates | null => {
  const parsedLatitude = Number(latitudeValue)
  const parsedLongitude = Number(longitudeValue)

  if (!Number.isFinite(parsedLatitude) || !Number.isFinite(parsedLongitude)) {
    return null
  }

  if (!isLatitudeInRange(parsedLatitude) || !isLongitudeInRange(parsedLongitude)) {
    return null
  }

  return {
    latitude: parsedLatitude,
    longitude: parsedLongitude,
  }
}

const formatCoordinate = (value: number) => value.toFixed(7)

function MapClickHandler({ onPick }: { onPick: (latitude: number, longitude: number) => void }) {
  useMapEvents({
    click(event) {
      onPick(event.latlng.lat, event.latlng.lng)
    },
  })

  return null
}

function RecenterMap({ center, zoom }: { center: LatLngTuple; zoom: number }) {
  const map = useMap()

  useEffect(() => {
    map.setView(center, zoom, { animate: false })
  }, [center, map, zoom])

  return null
}

function App() {
  const [latitude, setLatitude] = useState<string>('')
  const [longitude, setLongitude] = useState<string>('')
  const [selectedCoordinates, setSelectedCoordinates] = useState<Coordinates | null>(null)
  const [details, setDetails] = useState<string>('')
  const [locationMessage, setLocationMessage] = useState<string>(() =>
    navigator.geolocation
      ? 'Trying to read your location…'
      : 'Location services are unavailable on this device. Enter coordinates manually.',
  )
  const [submitState, setSubmitState] = useState<SubmitState>('idle')
  const [submitMessage, setSubmitMessage] = useState<string>('')

  useEffect(() => {
    if (!navigator.geolocation) {
      return
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        const detectedLatitude = position.coords.latitude
        const detectedLongitude = position.coords.longitude

        setLatitude(formatCoordinate(detectedLatitude))
        setLongitude(formatCoordinate(detectedLongitude))
        setSelectedCoordinates({ latitude: detectedLatitude, longitude: detectedLongitude })
        setLocationMessage('Location found. Please confirm or edit before submitting.')
      },
      () => {
        setLocationMessage('Could not read location. Enter coordinates manually.')
      },
      { enableHighAccuracy: true, timeout: 10000 },
    )
  }, [])

  const updateCoordinatesFromInputs = (nextLatitude: string, nextLongitude: string) => {
    const parsedCoordinates = parseCoordinates(nextLatitude, nextLongitude)

    if (parsedCoordinates) {
      setSelectedCoordinates(parsedCoordinates)
    }
  }

  const handleLatitudeChange = (value: string) => {
    setLatitude(value)
    updateCoordinatesFromInputs(value, longitude)
  }

  const handleLongitudeChange = (value: string) => {
    setLongitude(value)
    updateCoordinatesFromInputs(latitude, value)
  }

  const handleMapPick = (nextLatitude: number, nextLongitude: number) => {
    setLatitude(formatCoordinate(nextLatitude))
    setLongitude(formatCoordinate(nextLongitude))
    setSelectedCoordinates({ latitude: nextLatitude, longitude: nextLongitude })
    setLocationMessage('Location selected on map. You can click again or edit coordinates manually.')
  }

  const mapCenter = useMemo<LatLngTuple>(
    () =>
      selectedCoordinates
        ? [selectedCoordinates.latitude, selectedCoordinates.longitude]
        : defaultMapCenter,
    [selectedCoordinates],
  )
  const mapZoom = selectedCoordinates ? selectedMapZoom : defaultMapZoom

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSubmitState('saving')
    setSubmitMessage('')

    try {
      const parsedCoordinates = parseCoordinates(latitude, longitude)
      if (!parsedCoordinates) {
        throw new Error('Latitude and longitude must be valid numbers.')
      }

      await apiClient.post('/api/sightings', {
        latitude: parsedCoordinates.latitude,
        longitude: parsedCoordinates.longitude,
        details,
      })

      setSubmitState('saved')
      setSubmitMessage('Thank you. Gilly’s location report has been sent.')
      setDetails('')
    } catch (error) {
      setSubmitState('failed')
      setSubmitMessage(error instanceof Error ? error.message : 'Unable to submit report right now.')
    }
  }

  return (
    <main className="app-shell">
      <h1>Report Gilly’s Location</h1>
      <p className="hint">{locationMessage}</p>
      <section className="map-section" aria-label="Location map">
        <p className="map-hint">Click the map to choose the location, or type latitude and longitude manually.</p>
        <MapContainer center={mapCenter} zoom={mapZoom} scrollWheelZoom className="location-map">
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <MapClickHandler onPick={handleMapPick} />
          <RecenterMap center={mapCenter} zoom={mapZoom} />
          {selectedCoordinates && (
            <CircleMarker
              center={[selectedCoordinates.latitude, selectedCoordinates.longitude]}
              radius={8}
              pathOptions={{ color: '#0f766e', fillColor: '#14b8a6', fillOpacity: 0.85 }}
            />
          )}
        </MapContainer>
      </section>
      <form onSubmit={submit} className="report-form">
        <label>
          Latitude
          <input
            type="number"
            step="any"
            value={latitude}
            onChange={(event) => handleLatitudeChange(event.target.value)}
            required
          />
        </label>
        <label>
          Longitude
          <input
            type="number"
            step="any"
            value={longitude}
            onChange={(event) => handleLongitudeChange(event.target.value)}
            required
          />
        </label>
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
          {submitState === 'saving' ? 'Sending…' : 'Send report'}
        </button>
        {submitMessage && <p className={submitState === 'failed' ? 'error' : 'success'}>{submitMessage}</p>}
      </form>
    </main>
  )
}

export default App
