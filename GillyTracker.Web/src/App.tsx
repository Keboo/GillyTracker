import { FormEvent, useEffect, useState } from 'react'
import { apiClient } from './services/apiClient'

type SubmitState = 'idle' | 'saving' | 'saved' | 'failed'

function App() {
  const [latitude, setLatitude] = useState<string>('')
  const [longitude, setLongitude] = useState<string>('')
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
        setLatitude(position.coords.latitude.toFixed(7))
        setLongitude(position.coords.longitude.toFixed(7))
        setLocationMessage('Location found. Please confirm or edit before submitting.')
      },
      () => {
        setLocationMessage('Could not read location. Enter coordinates manually.')
      },
      { enableHighAccuracy: true, timeout: 10000 },
    )
  }, [])

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSubmitState('saving')
    setSubmitMessage('')

    try {
      const parsedLatitude = Number(latitude)
      const parsedLongitude = Number(longitude)

      if (Number.isNaN(parsedLatitude) || Number.isNaN(parsedLongitude)) {
        throw new Error('Latitude and longitude must be valid numbers.')
      }

      await apiClient.post('/api/sightings', {
        latitude: parsedLatitude,
        longitude: parsedLongitude,
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
      <form onSubmit={submit} className="report-form">
        <label>
          Latitude
          <input
            type="number"
            step="any"
            value={latitude}
            onChange={(event) => setLatitude(event.target.value)}
            required
          />
        </label>
        <label>
          Longitude
          <input
            type="number"
            step="any"
            value={longitude}
            onChange={(event) => setLongitude(event.target.value)}
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
