import http from './client.js'

// Use local calendar date/time, matching the backend's calendar-day contract.
export function localDate(date = new Date()) {
  return [date.getFullYear(), String(date.getMonth() + 1).padStart(2, '0'), String(date.getDate()).padStart(2, '0')].join('-')
}
export function localTime(date = new Date()) {
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}
export const getCurrentDay = date => http.get('/api/work-days/current', { params: { date } }).then(r => r.data)
export const getHistory = (from, to) => http.get('/api/work-days/history', { params: { from, to } }).then(r => r.data)
export const startDay = () => http.post('/api/work-days/start', { date: localDate(), startTime: localTime() }).then(r => r.data)
export const finishDay = id => http.post(`/api/work-days/${id}/finish`, { endTime: localTime() }).then(r => r.data)
export const listBreaks = id => http.get(`/api/work-days/${id}/breaks`).then(r => r.data)
export const startBreak = id => http.post(`/api/work-days/${id}/breaks/start`, { startTime: localTime() }).then(r => r.data)
export const finishBreak = id => http.post(`/api/breaks/${id}/finish`, { endTime: localTime() }).then(r => r.data)
export const submitReport = (dayId, text) => http.post(`/api/reports/${dayId}`, { text }).then(r => r.data)
export const searchReports = params => http.get('/api/reports/search', { params }).then(r => r.data)
export const attendanceSummary = (date, departmentId) => http.get('/api/attendance/summary', { params: { date, ...(departmentId ? { departmentId } : {}) } }).then(r => r.data)
export const usersList = () => http.get('/api/users', { params: { includeInactive: false } }).then(r => r.data)
export const departmentsList = () => http.get('/api/departments').then(r => r.data)
export const getDaily = id => http.get(`/api/reports/daily/${id}`).then(r => r.data)

export const addReportComment = (workDayId, reportId, text) => http.post(`/api/reports/${workDayId}/comments`, { reportId, text }).then(r => r.data)
export const listReportComments = workDayId => http.get(`/api/reports/${workDayId}/comments`).then(r => r.data)
