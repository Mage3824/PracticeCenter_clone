import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { loginApi } from '@/api/auth.js'

function jwtClaims(token) {
  try {
    const payload = token.split('.')[1]
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    return JSON.parse(atob(base64))
  } catch { return null }
}
function tokenFromStorage() {
  return sessionStorage.getItem('accessToken') || localStorage.getItem('accessToken') || ''
}
const roleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
const userClaim = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref(tokenFromStorage())
  const claims = computed(() => jwtClaims(accessToken.value))
  const userId = computed(() => claims.value?.[userClaim] || claims.value?.nameid || claims.value?.sub || '')
  const login = computed(() => claims.value?.unique_name || claims.value?.name || '')
  const fullName = computed(() => login.value)
  const role = computed(() => claims.value?.[roleClaim] || claims.value?.role || null)
  const isLoggedIn = computed(() => !!accessToken.value && !!claims.value?.exp && claims.value.exp * 1000 > Date.now())

  async function tryLogin(userLogin, password, remember = false) {
    try {
      const result = await loginApi(userLogin.trim(), password)
      if (!result.isSuccess || !result.accessToken) {
        return { success: false, message: result.message || 'Неверный логин или пароль',
          requiresCaptcha: result.requiresCaptcha, isLocked: result.isLocked, lockedUntil: result.lockedUntil }
      }
      const storage = remember ? localStorage : sessionStorage
      sessionStorage.removeItem('accessToken')
      localStorage.removeItem('accessToken')
      storage.setItem('accessToken', result.accessToken)
      accessToken.value = result.accessToken
      return { success: true }
    } catch (error) {
      return { success: false, message: error.message || 'API недоступен' }
    }
  }
  function logout() {
    sessionStorage.removeItem('accessToken')
    localStorage.removeItem('accessToken')
    accessToken.value = ''
  }
  return { accessToken, userId, login, fullName, role, isLoggedIn, tryLogin, logout }
})
