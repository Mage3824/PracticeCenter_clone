import axios from 'axios'

// All API calls go through Vite's same-origin proxy during development.
const http = axios.create({ baseURL: '/', timeout: 15000 })

http.interceptors.request.use((config) => {
  const token = sessionStorage.getItem('accessToken') || localStorage.getItem('accessToken')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

http.interceptors.response.use(
  response => response,
  (error) => {
    const data = error.response?.data
    const message = data?.error?.message || data?.detail || data?.message ||
      (error.response?.status === 401 ? 'Сессия истекла. Войдите ещё раз.' : 'Нет связи с сервером API.')
    const wrapped = new Error(message)
    wrapped.code = data?.error?.code || data?.code || null
    wrapped.status = error.response?.status || 0
    return Promise.reject(wrapped)
  }
)
export default http
