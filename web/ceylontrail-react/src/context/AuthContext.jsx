import { createContext, useMemo, useState } from 'react'
import { login as loginRequest } from '../api/auth'

const AUTH_STORAGE_KEY = 'ceylontrail.auth'
const AuthContext = createContext(null)

function readStoredAuth() {
  try {
    const storedAuth = localStorage.getItem(AUTH_STORAGE_KEY)
    if (!storedAuth) return null
    const auth = JSON.parse(storedAuth)
    if (!auth?.token || !auth?.user || new Date(auth.expiresAt) <= new Date()) {
      localStorage.removeItem(AUTH_STORAGE_KEY)
      return null
    }
    return auth
  } catch {
    localStorage.removeItem(AUTH_STORAGE_KEY)
    return null
  }
}

export function AuthProvider({ children }) {
  const [auth, setAuth] = useState(readStoredAuth)

  async function login(credentials) {
    const response = await loginRequest(credentials)
    const nextAuth = { token: response.token, expiresAt: response.expiresAt, user: response.user }
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(nextAuth))
    setAuth(nextAuth)
    return nextAuth
  }

  function logout() {
    localStorage.removeItem(AUTH_STORAGE_KEY)
    setAuth(null)
  }

  const value = useMemo(() => ({
    auth,
    user: auth?.user ?? null,
    isAuthenticated: Boolean(auth),
    isLoading: false,
    login,
    logout,
  }), [auth])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export { AuthContext }
