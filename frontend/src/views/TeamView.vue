<template>
  <div class="page">
    <header class="header">
      <div class="header-left">
        <span class="logo">Программный центр</span>
        <nav class="nav">
          <router-link to="/dashboard" class="nav-link">Мой день</router-link>
          <router-link to="/reports" class="nav-link">Мои отчёты</router-link>
          <router-link to="/manager" class="nav-link">Дашборд руководителя</router-link>
          <router-link to="/team" class="nav-link active">Команда</router-link>
          <router-link v-if="auth.role === 'Admin'" to="/users" class="nav-link">Пользователи</router-link>
          <router-link v-if="auth.role === 'Admin'" to="/system" class="nav-link">Система</router-link>
        </nav>
      </div>

      <div class="header-right">
        <div class="avatar">{{ avatarLetters }}</div>
        <div class="user-info">
          <div class="user-name">{{ auth.fullName || auth.login }}</div>
          <div class="user-role">{{ roleLabel }}</div>
        </div>
      </div>
    </header>

    <main class="main-content">
      <div class="page-header">
        <h1>Отчёты команды</h1>
        <div class="current-date">{{ formattedDate }}</div>
      </div>

      <div class="filters-card">
        <div class="filters-row">
          <div class="filter-group">
            <label class="filter-label">Подразделение</label>
            <select v-model="selectedDepartment" class="filter-select">
              <option value="">Все подразделения</option>
              <option v-for="d in departments" :key="d.id" :value="d.id">
                {{ d.name }}
              </option>
            </select>
          </div>

          <div class="filter-group">
            <label class="filter-label">Сотрудник</label>
            <select v-model="selectedEmployee" class="filter-select">
              <option value="">Все сотрудники</option>
              <option v-for="e in employees" :key="e.id" :value="e.id">
                {{ e.fullName }}
              </option>
            </select>
          </div>

          <div class="filter-group">
            <label class="filter-label">Период</label>
            <div class="period-select" @click="showPeriodMenu = !showPeriodMenu">
              <span class="calendar-icon">📅</span>
              <span>{{ periodLabel }}</span>
              <span class="chevron">▾</span>
            </div>
          </div>
        </div>

        <button class="btn-export" @click="onExport">
          <span class="export-icon">⬇</span>
          Экспорт CSV
        </button>
      </div>

      <div class="filters-card" style="margin-top:12px">
        <label class="filter-label">Поиск по тексту отчёта</label>
        <input v-model="searchText" class="filter-select" placeholder="Например: тестирование" />
        <button class="btn-export" @click="loadReports">Обновить</button>
        <p v-if="apiError" role="alert" style="color:#b91c1c">{{ apiError }}</p>
        <p v-if="loading">Загрузка отчётов с сервера…</p>
      </div>
      <div class="reports-section">
        <div class="section-header">
          <h2 class="section-title">
            Отчёты за период
            <span class="count-badge">{{ filteredReports.length }}</span>
          </h2>
          <button class="btn-sort" @click="toggleSort">
            <span class="sort-icon">⇅</span>
            {{ sortLabel }}
          </button>
        </div>

        <div v-if="filteredReports.length === 0" class="empty-state">
          Нет отчётов за выбранный период
        </div>

        <div v-for="report in enrichedReports" :key="report.id" class="report-card">
          <div class="report-main">
            <div class="report-author">
              <div class="author-avatar">{{ report.authorAvatar }}</div>
              <div class="author-info">
                <div class="author-name">{{ report.authorName }}</div>
                <div class="author-role">{{ report.authorRole }}</div>
              </div>
            </div>

            <div class="report-meta">
              <div class="meta-hours">
                <span class="hours-icon">🕐</span>
                {{ report.workedHoursText }}
              </div>
              <div class="meta-date">{{ report.submittedAtFormatted }}</div>
              <span class="status-badge sent">Отправлен</span>
            </div>
          </div>

          <div class="report-text">{{ report.text }}</div>

          <div class="comments-section">
            <div class="comments-header">
              <h3 class="comments-title">
                Комментарии
                <span class="count-badge">{{ report.comments.length }}</span>
              </h3>
              <button class="btn-add-comment" @click="openCommentModal(report)">
                + Добавить комментарий
              </button>
            </div>

            <div v-if="report.comments.length > 0" class="comments-list">
              <div v-for="c in report.comments" :key="c.id" class="comment-item">
                <div class="comment-avatar">{{ c.avatar }}</div>
                <div class="comment-body">
                  <div class="comment-author">
                    <span class="comment-author-name">{{ c.authorName }}</span>
                    <span class="comment-date">{{ c.dateFormatted }}</span>
                  </div>
                  <div class="comment-text">{{ c.text }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>

    <div v-if="showPeriodMenu" class="dropdown-menu" @click.self="showPeriodMenu = false">
      <div
        v-for="p in periods"
        :key="p.value"
        class="dropdown-item"
        :class="{ active: selectedPeriod === p.value }"
        @click="selectedPeriod = p.value; showPeriodMenu = false"
      >
        {{ p.label }}
      </div>
    </div>

    <div v-if="showCommentModal" class="modal-overlay" @click.self="showCommentModal = false">
      <div class="modal">
        <h3>Добавить комментарий</h3>
        <textarea v-model="newCommentText" class="modal-textarea" placeholder="Текст комментария..."></textarea>
        <div class="modal-actions">
          <button class="btn btn-cancel" @click="showCommentModal = false">Отмена</button>
          <button class="btn btn-confirm" @click="addComment" :disabled="!newCommentText.trim()">Добавить</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth.js'
import { departmentsList, usersList, searchReports, getDaily, addReportComment, listReportComments, localDate } from '@/api/worktime.js'

const auth = useAuthStore()

const departments = ref([])
const employees = ref([])
const searchText = ref('')
const loading = ref(false)
const apiError = ref('')
const periods = [
  { value: 'week', label: 'Текущая неделя' },
  { value: 'prev-week', label: 'Прошлая неделя' },
  { value: 'month', label: 'Текущий месяц' }
]
const selectedDepartment = ref('')
const selectedEmployee = ref('')
const selectedPeriod = ref('week')
const showPeriodMenu = ref(false)
const sortNewest = ref(true)
const showCommentModal = ref(false)
const newCommentText = ref('')
const currentReportId = ref(null)

const periodLabel = computed(() => {
  return periods.find(p => p.value === selectedPeriod.value)?.label || ''
})

const sortLabel = computed(() => sortNewest.value ? 'Сначала новые' : 'Сначала старые')

const reports = ref([])
function periodRange() {
  const today = new Date()
  const start = new Date(today)
  const end = new Date(today)
  const mondayOffset = (today.getDay() + 6) % 7
  if (selectedPeriod.value === 'month') start.setDate(1)
  else if (selectedPeriod.value === 'prev-week') {
    start.setDate(start.getDate() - mondayOffset - 7)
    end.setDate(end.getDate() - mondayOffset - 1)
  } else start.setDate(start.getDate() - mondayOffset)
  return { from: localDate(start), to: localDate(end) }
}
async function loadReports() {
  loading.value = true
  apiError.value = ''
  try {
    const result = await searchReports({
      ...periodRange(),
      text: searchText.value.trim() || undefined,
      employeeId: selectedEmployee.value || undefined,
      departmentId: selectedDepartment.value || undefined
    })
    const roster = new Map(employees.value.map(e => [e.id, e]))
    reports.value = await Promise.all(result.map(async item => {
      const [daily, comments] = await Promise.all([
        getDaily(item.workDayId).catch(() => null),
        listReportComments(item.workDayId).catch(() => [])
      ])
      const name = item.employeeName || 'Сотрудник'
      return {
        id: item.id, workDayId: item.workDayId,
        authorId: item.employeeId,
        authorName: name,
        authorRole: roster.get(item.employeeId)?.role || 'Сотрудник',
        authorAvatar: name.split(' ').map(x => x[0]).join('').slice(0, 2).toUpperCase(),
        departmentId: item.departmentId,
        workedMinutes: daily?.totalMinutes || 0,
        submittedAt: item.createdAt,
        text: item.text,
        comments: comments.map(c => ({ id: c.id, authorName: roster.get(c.managerId)?.fullName || 'Руководитель',
          avatar: 'Р', date: c.createdAt, text: c.text }))
      }
    }))
  } catch (error) {
    apiError.value = error.message || 'Не удалось загрузить отчёты.'
    reports.value = []
  } finally { loading.value = false }
}
async function initialize() {
  try {
    const [deps, roster] = await Promise.all([departmentsList(), usersList()])
    departments.value = deps
    employees.value = roster
  } catch (error) { apiError.value = error.message }
  await loadReports()
}
watch([selectedDepartment, selectedEmployee, selectedPeriod, searchText], loadReports)
onMounted(initialize)

const filteredReports = computed(() => {
  let list = reports.value
  if (selectedDepartment.value) {
    list = list.filter(r => r.departmentId === selectedDepartment.value)
  }
  if (selectedEmployee.value) {
    list = list.filter(r => r.authorId === selectedEmployee.value)
  }
  if (sortNewest.value) {
    list = [...list].sort((a, b) => new Date(b.submittedAt) - new Date(a.submittedAt))
  } else {
    list = [...list].sort((a, b) => new Date(a.submittedAt) - new Date(b.submittedAt))
  }
  return list
})

function toggleSort() {
  sortNewest.value = !sortNewest.value
}

function onExport() {
  const lines = [['Сотрудник', 'Дата', 'Отработано (мин)', 'Отчёт'],
    ...filteredReports.value.map(r => [r.authorName, r.submittedAt, r.workedMinutes, r.text])]
  const csv = '\ufeff' + lines.map(row => row.map(v => '"' + String(v).replace(/^([=+@-])/, "'$1").replaceAll('"', '""') + '"').join(';')).join('\r\n')
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
  const link = document.createElement('a')
  link.href = url
  link.download = `reports-${localDate()}.csv`
  link.click()
  URL.revokeObjectURL(url)
}

function openCommentModal(report) {
  currentReportId.value = report.id
  newCommentText.value = ''
  showCommentModal.value = true
}

async function addComment() {
  if (!newCommentText.value.trim()) return
  const report = reports.value.find(r => r.id === currentReportId.value)
  if (!report) return
  try {
    await addReportComment(report.workDayId, report.id, newCommentText.value.trim())
    showCommentModal.value = false
    await loadReports()
  } catch (error) { apiError.value = error.message }
}

const dayNames = ['Воскресенье', 'Понедельник', 'Вторник', 'Среда', 'Четверг', 'Пятница', 'Суббота']
const monthNames = ['января', 'февраля', 'марта', 'апреля', 'мая', 'июня', 'июля', 'августа', 'сентября', 'октября', 'ноября', 'декабря']

const formattedDate = computed(() => {
  const d = new Date()
  return `${dayNames[d.getDay()]}, ${d.getDate()} ${monthNames[d.getMonth()]} ${d.getFullYear()}`
})

const avatarLetters = computed(() => {
  const name = auth.fullName || auth.login || 'U'
  return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2)
})

const roleLabel = computed(() => {
  const roles = { 'Employee': 'Разработчик', 'Manager': 'Руководитель отдела', 'Admin': 'Администратор' }
  return roles[auth.role] || 'Сотрудник'
})

function formatWorkedHours(minutes) {
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return `${h} ч ${m} мин`
}

function formatDateTime(iso) {
  const d = new Date(iso)
  const date = `${d.getDate()} ${monthNames[d.getMonth()]} ${d.getFullYear()}`
  const time = `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
  return `${date}, ${time}`
}

const enrichedReports = computed(() => {
  return filteredReports.value.map(r => ({
    ...r,
    workedHoursText: formatWorkedHours(r.workedMinutes),
    submittedAtFormatted: formatDateTime(r.submittedAt),
    comments: r.comments.map(c => ({
      ...c,
      dateFormatted: formatDateTime(c.date)
    }))
  }))
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  background: #f8f9fa;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 40px;
  height: 64px;
  background: white;
  border-bottom: 1px solid #e5e7eb;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 40px;
}

.logo {
  font-size: 16px;
  font-weight: 600;
  color: #1a1a1a;
}

.nav {
  display: flex;
  gap: 8px;
}

.nav-link {
  text-decoration: none;
  color: #6b7280;
  font-size: 14px;
  padding: 8px 16px;
  border-radius: 6px;
  transition: all 0.2s;
}

.nav-link:hover {
  color: #1a1a1a;
  background: #f3f4f6;
}

.nav-link.active {
  color: #2563eb;
  font-weight: 500;
  border-bottom: 2px solid #2563eb;
  border-radius: 0;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: #dbeafe;
  color: #2563eb;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 600;
}

.user-info {
  display: flex;
  flex-direction: column;
}

.user-name {
  font-size: 14px;
  font-weight: 500;
  color: #1a1a1a;
}

.user-role {
  font-size: 12px;
  color: #6b7280;
}

.main-content {
  max-width: 1200px;
  margin: 0 auto;
  padding: 32px 40px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-header h1 {
  font-size: 28px;
  font-weight: 700;
  color: #1a1a1a;
  margin: 0;
}

.current-date {
  font-size: 14px;
  color: #6b7280;
}

.filters-card {
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 24px;
  margin-bottom: 24px;
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 24px;
}

.filters-row {
  display: flex;
  gap: 16px;
  flex: 1;
}

.filter-group {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.filter-label {
  font-size: 12px;
  color: #6b7280;
}

.filter-select {
  padding: 10px 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font-size: 14px;
  color: #1a1a1a;
  background: white;
  outline: none;
  cursor: pointer;
}

.filter-select:focus {
  border-color: #2563eb;
}

.period-select {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font-size: 14px;
  color: #1a1a1a;
  cursor: pointer;
  background: white;
}

.period-select:hover {
  border-color: #2563eb;
}

.calendar-icon {
  font-size: 16px;
}

.chevron {
  font-size: 12px;
  color: #9ca3af;
}

.btn-export {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 20px;
  background: #2563eb;
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  white-space: nowrap;
}

.btn-export:hover {
  background: #1d4ed8;
}

.export-icon {
  font-size: 14px;
}

.reports-section {
  margin-bottom: 32px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.section-title {
  font-size: 18px;
  font-weight: 600;
  color: #1a1a1a;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.count-badge {
  background: #f3f4f6;
  color: #6b7280;
  font-size: 12px;
  font-weight: 500;
  padding: 2px 8px;
  border-radius: 10px;
}

.btn-sort {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  font-size: 13px;
  color: #4b5563;
  cursor: pointer;
}

.btn-sort:hover {
  border-color: #2563eb;
  color: #2563eb;
}

.sort-icon {
  font-size: 14px;
}

.empty-state {
  text-align: center;
  padding: 40px;
  color: #6b7280;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
}

.report-card {
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 24px;
  margin-bottom: 16px;
}

.report-main {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.report-author {
  display: flex;
  align-items: center;
  gap: 12px;
}

.author-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: #dbeafe;
  color: #2563eb;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 600;
}

.author-info {
  display: flex;
  flex-direction: column;
}

.author-name {
  font-size: 15px;
  font-weight: 600;
  color: #1a1a1a;
}

.author-role {
  font-size: 12px;
  color: #6b7280;
}

.report-meta {
  display: flex;
  align-items: center;
  gap: 16px;
}

.meta-hours {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  font-weight: 500;
  color: #1a1a1a;
}

.hours-icon {
  font-size: 16px;
}

.meta-date {
  font-size: 13px;
  color: #6b7280;
}

.status-badge {
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 500;
}

.status-badge.sent {
  background: #dbeafe;
  color: #1e40af;
}

.report-text {
  font-size: 14px;
  color: #4b5563;
  line-height: 1.5;
  padding: 12px 16px;
  background: #f9fafb;
  border-radius: 8px;
  margin-bottom: 20px;
}

.comments-section {
  border-top: 1px solid #f3f4f6;
  padding-top: 16px;
}

.comments-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.comments-title {
  font-size: 14px;
  font-weight: 600;
  color: #1a1a1a;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.btn-add-comment {
  padding: 8px 16px;
  background: #2563eb;
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
}

.btn-add-comment:hover {
  background: #1d4ed8;
}

.comments-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.comment-item {
  display: flex;
  gap: 12px;
  padding: 12px 16px;
  background: #f9fafb;
  border-radius: 8px;
}

.comment-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: #dbeafe;
  color: #2563eb;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 600;
  flex-shrink: 0;
}

.comment-body {
  flex: 1;
}

.comment-author {
  display: flex;
  gap: 12px;
  align-items: baseline;
  margin-bottom: 4px;
}

.comment-author-name {
  font-size: 13px;
  font-weight: 600;
  color: #1a1a1a;
}

.comment-date {
  font-size: 12px;
  color: #6b7280;
}

.comment-text {
  font-size: 13px;
  color: #4b5563;
  line-height: 1.4;
}

.dropdown-menu {
  position: fixed;
  top: 100px;
  right: 40px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  z-index: 100;
  min-width: 220px;
}

.dropdown-item {
  padding: 10px 16px;
  font-size: 14px;
  color: #4b5563;
  cursor: pointer;
}

.dropdown-item:hover {
  background: #f3f4f6;
}

.dropdown-item.active {
  color: #2563eb;
  font-weight: 500;
}

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal {
  background: white;
  border-radius: 12px;
  padding: 32px;
  max-width: 500px;
  width: 90%;
}

.modal h3 {
  margin: 0 0 16px;
  font-size: 18px;
  color: #1a1a1a;
}

.modal-textarea {
  width: 100%;
  min-height: 100px;
  padding: 12px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font-size: 14px;
  font-family: inherit;
  resize: vertical;
  box-sizing: border-box;
  margin-bottom: 16px;
}

.modal-textarea:focus {
  outline: none;
  border-color: #2563eb;
}

.modal-actions {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
}

.btn {
  padding: 10px 20px;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
}

.btn-cancel {
  background: #f3f4f6;
  color: #4b5563;
}

.btn-cancel:hover {
  background: #e5e7eb;
}

.btn-confirm {
  background: #2563eb;
  color: white;
}

.btn-confirm:hover:not(:disabled) {
  background: #1d4ed8;
}

.btn-confirm:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>