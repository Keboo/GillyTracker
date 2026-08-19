import { useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import UploadFileIcon from '@mui/icons-material/UploadFile'
import { Button, IconButton } from '@mui/material'
import { apiClient } from '@/services/apiClient'
import type { ImportSightingsResponse } from '@/types'

export default function ImportSightings() {
  const navigate = useNavigate()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [uploading, setUploading] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string>('')
  const [result, setResult] = useState<ImportSightingsResponse | null>(null)

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null
    setSelectedFile(file)
    setResult(null)
    setErrorMessage('')
  }

  const handleUpload = async () => {
    if (!selectedFile) {
      setErrorMessage('Choose a CSV file to import.')
      return
    }

    setUploading(true)
    setErrorMessage('')
    setResult(null)

    try {
      const formData = new FormData()
      formData.append('file', selectedFile)
      const response = await apiClient.postForm<ImportSightingsResponse>('/api/sightings/import', formData)
      setResult(response)
      setSelectedFile(null)
      if (fileInputRef.current) {
        fileInputRef.current.value = ''
      }
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : 'Unable to import sightings.')
    } finally {
      setUploading(false)
    }
  }

  return (
    <main className="app-shell">
      <div className="detail-header">
        <IconButton
          className="detail-back-button"
          aria-label="Back to sightings"
          onClick={() => navigate('/admin/sightings')}
          size="large"
          title="Back to sightings"
        >
          <ArrowBackIcon fontSize="inherit" />
        </IconButton>
        <h1>Import Sightings</h1>
      </div>

      <p className="hint">
        Upload a CSV file with <code>Latitude</code> and <code>Longitude</code> columns. Optional
        <code> Details</code> and <code>CreatedDate</code> columns are also supported.
      </p>

      <div className="import-form">
        <label htmlFor="csv-file-input">CSV file</label>
        <input
          ref={fileInputRef}
          id="csv-file-input"
          type="file"
          accept=".csv,text/csv"
          onChange={handleFileChange}
        />

        <Button
          variant="contained"
          className="report-submit-button"
          startIcon={<UploadFileIcon />}
          disabled={!selectedFile || uploading}
          onClick={() => void handleUpload()}
        >
          {uploading ? 'Importing...' : 'Import CSV'}
        </Button>
      </div>

      {errorMessage && <p className="error">{errorMessage}</p>}

      {result && (
        <div className="import-result">
          <p className="success">Imported {result.importedCount} sighting{result.importedCount === 1 ? '' : 's'}.</p>
          {result.errors.length > 0 && (
            <>
              <p className="error">{result.errors.length} row{result.errors.length === 1 ? '' : 's'} could not be imported:</p>
              <ul className="import-errors">
                {result.errors.map((rowError) => (
                  <li key={rowError.lineNumber}>
                    Line {rowError.lineNumber}: {rowError.message}
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>
      )}
    </main>
  )
}
