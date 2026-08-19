export interface UserInfo {
  userId: string
  userName: string
  email: string
  isAuthenticated: boolean
  isAdmin: boolean
}

export interface SightingResponse {
  id: string
  latitude: number
  longitude: number
  details?: string
  createdDate: string
}

export interface ApiResponse<T = void> {
  success: boolean
  data?: T
  errors?: string[]
}

export interface ImportSightingsRowError {
  lineNumber: number
  message: string
}

export interface ImportSightingsResponse {
  importedCount: number
  errors: ImportSightingsRowError[]
}
