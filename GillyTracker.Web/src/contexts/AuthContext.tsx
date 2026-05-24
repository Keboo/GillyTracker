import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { UserInfo } from '@/types'
import { apiClient } from '@/services/apiClient'

interface AuthContextType {
  user: UserInfo | null
  loading: boolean
  beginMicrosoftLogin: (returnUrl?: string) => void
  logout: () => Promise<void>
  refreshUser: () => Promise<void>
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null)
  const [loading, setLoading] = useState(true)

  const refreshUser = useCallback(async () => {
    try {
      const userData = await apiClient.get<UserInfo>('/api/auth/user')
      setUser(userData)
    } catch {
      setUser(null)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- bootstrap auth state from server session
    void refreshUser()
  }, [refreshUser])

  const beginMicrosoftLogin = (returnUrl = '/admin/sightings') => {
    const target = `/api/auth/microsoft/login?returnUrl=${encodeURIComponent(returnUrl)}`
    window.location.assign(target)
  }

  const logout = async () => {
    await apiClient.post('/api/auth/logout')
    setUser(null)
  }

  return (
    <AuthContext.Provider value={{ user, loading, beginMicrosoftLogin, logout, refreshUser }}>
      {children}
    </AuthContext.Provider>
  )
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext)
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
