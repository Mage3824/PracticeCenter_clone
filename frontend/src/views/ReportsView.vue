<template>
  <div class="page">
    <header class="header">
      <div class="header-left">
        <span class="logo">Программный центр</span>
        <nav class="nav">
          <router-link to="/dashboard" class="nav-link">Мой день</router-link>
          <router-link to="/reports" class="nav-link active">Мои отчёты</router-link>
          <router-link v-if="auth.role !== 'Employee'" to="/manager" class="nav-link">Дашборд руководителя</router-link>
          <router-link v-if="auth.role !== 'Employee'" to="/team" class="nav-link" active-class="active">Команда</router-link>
          <router-link v-if="auth.role === 'Admin'" to="/users" class="nav-link">Пользователи</router-link>
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
        <h1>Мои отчёты</h1>
        <div class="period-selector" @click="showPeriodMenu = !showPeriodMenu">
          <span class="calendar-icon">📅</span>
          <span>{{ periodLabel }}</span>
          <span class="chevron">▾</span>
        </div>
      </div>

      <p v-if="apiError" role="alert" style="color:#b91c1c">{{ apiError }}</p>
      <p v-if="loading">Загрузка истории с сервера…</p>
      <div class="summary-cards">
        <div class="card">
          <div class="card-header">
            <span class="card-title">Рабочих дней</span>
            <span class="card-icon icon-blue">📅</span>
          </div>
          <div class="card-value blue">{{ summary.workDays }}</div>
        </div>

        <div class="card">
          <div class="card-header">
            <span class="card-title">Часов</span>
            <span class="card-icon icon-green">🕐</span>
          </div>
          <div class="card-value green">{{ summary.totalHours }}</div>
        </div>

        <div class="card">
          <div class="card-header">
            <span class="card-title">Перерывов</span>
            <span class="card-icon icon-orange">☕</span>
          </div>
          <div class="card-value orange">{{ summary.breaks }}</div>
        </div>
      </div>

      <div class="reports-table-card">
        <div class="table-header">
          <div class="table-title">
            Отчёты по дням
            <span class="count-badge">{{ filteredReports.length }}</span>
          </div>
          <div class="table-actions">
            <div class="filter-dropdown" @click="showStatusFilter = !showStatusFilter">
              <span>{{ statusFilterLabel }}</span>
              <span class="chevron">▾</span>
            </div>
            <button class="btn-download" @click="onDownload">
              <span class="download-icon">⬇</span>
              Скачать за период
            </button>
          </div>
        </div>

        <table class="reports-table">
          <thead>
            <tr>
              <th>Дата</th>
              <th>Приход</th>
              <th>Уход</th>
              <th>Статус</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="report in filteredReports" :key="report.id">
              <td>
                <div class="date-cell">
                  <div class="date-main">{{ report.dateFormatted }}</div>
                  <div class="date-weekday">{{ report.dayOfWeek }}</div>
                </div>
              </td>
              <td class="time-cell">{{ report.startTime || '—' }}</td>
              <td class="time-cell">{{ report.endTime || '—' }}</td>
              <td>
                <span class="status-badge" :class="report.statusClass">
                  {{ report.statusLabel }}
                </span>
              </td>
              <td>
                <router-link :to="`/reports/${report.id}`" class="btn-view">
                  Посмотреть отчёт
                </router-link>
              </td>
            </tr>
          </tbody>
        </table>

        <div class="table-footer">
          <div class="footer-left">
            Показано {{ filteredReports.length }} из {{ reports.length }} отчётов
          </div>
          <div class="footer-right">
            Итого за период: <strong>{{ summary.totalHoursText }}</strong>
          </div>
        </div>
      </div>
    </main>

    <div v-if="showPeriodMenu" class="dropdown-menu period-menu" @click.self="showPeriodMenu = false">
      <div
        v-for="p in periods"
        :key="p.value"
        class="dropdown-item"
        :class="{ active: selectedPeriod === p.value }"
        @click="selectPeriod(p.value)"
      >
        {{ p.label }}
      </div>
    </div>

    <div v-if="showStatusFilter" class="dropdown-menu status-menu" @click.self="showStatusFilter = false">
      <div
        v-for="s in statusOptions"
        :key="s.value"
        class="dropdown-item"
        :class="{ active: statusFilter === s.value }"
        @click="selectStatus(s.value)"
      >
        {{ s.label }}
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useAuthStore } from '@/stores/auth.js'
import { getHistory, getDaily, localDate } from '@/api/worktime.js'

const auth = useAuthStore()

const reports = ref([])
const loading = ref(false)
const apiError = ref('')
const periods = [
  { value: 'current', label: 'Текущий месяц' },
  { value: 'prev', label: 'Прошлый месяц' },
  { value: 'sept', label: 'Последние 90 дней' }
]
async function loadHistory() {
  loading.value = true
  apiError.value = ''
  try {
    const now = new Date()
    let start = new Date(now.getFullYear(), now.getMonth(), 1)
    let end = now
    if (selectedPeriod.value === 'prev') {
      start = new Date(now.getFullYear(), now.getMonth() - 1, 1)
      end = new Date(now.getFullYear(), now.getMonth(), 0)
    }
    if (selectedPeriod.value === 'sept') {
      start = new Date(now)
      start.setDate(start.getDate() - 90)
    }
    const days = await getHistory(localDate(start), localDate(end))
    reports.value = await Promise.all(days.map(async day => {
      const daily = day.status === 'Active' ? null : await getDaily(day.id).catch(() => null)
      return { ...day, breakCount: daily?.breaks?.length || 0, totalMinutes: daily?.totalMinutes || 0 }
    }))
  } catch (error) { apiError.value = error.message || 'История не загружена'; reports.value = [] }
  finally { loading.value = false }
}
const selectedPeriod = ref('current')
const showPeriodMenu = ref(false)

const periodLabel = computed(() => {
  return periods.find(p => p.value === selectedPeriod.value)?.label || ''
})

function selectPeriod(value) {
  selectedPeriod.value = value
  showPeriodMenu.value = false
}

const statusOptions = [
  { value: 'all', label: 'Все статусы' },
  { value: 'Finished', label: 'Отработано' },
  { value: 'Active', label: 'Не завершён' },
  { value: 'Absent', label: 'Отсутствие' }
]
const statusFilter = ref('all')
const showStatusFilter = ref(false)

const statusFilterLabel = computed(() => {
  return statusOptions.find(s => s.value === statusFilter.value)?.label || 'Все статусы'
})

function selectStatus(value) {
  statusFilter.value = value
  showStatusFilter.value = false
}

const dayNames = ['Воскресенье', 'Понедельник', 'Вторник', 'Среда', 'Четверг', 'Пятница', 'Суббота']
const monthNames = ['января', 'февраля', 'марта', 'апреля', 'мая', 'июня', 'июля', 'августа', 'сентября', 'октября', 'ноября', 'декабря']

const statusMap = {
  Finished: { label: 'Отработано', class: 'completed' },
  Active: { label: 'Не завершён', class: 'partial' },
  Absent: { label: 'Отсутствие', class: 'absent' },
  AutoFinished: { label: 'Автозавершён', class: 'partial' }
}

const formattedReports = computed(() => {
  return reports.value.map(r => {
    const d = new Date(r.date)
    return {
      ...r,
      dateFormatted: `${d.getDate()} ${monthNames[d.getMonth()]} ${d.getFullYear()}`,
      dayOfWeek: dayNames[d.getDay()],
      statusLabel: statusMap[r.status]?.label || r.status,
      statusClass: statusMap[r.status]?.class || ''
    }
  })
})

const filteredReports = computed(() => {
  if (statusFilter.value === 'all') return formattedReports.value
  return formattedReports.value.filter(r => r.status === statusFilter.value)
})

const summary = computed(() => {
  const list = filteredReports.value
  const workDays = list.filter(r => r.status !== 'Absent').length
  const totalMinutes = list.reduce((sum, r) => sum + (r.totalMinutes || 0), 0)
  const totalHours = Math.floor(totalMinutes / 60)
  const breaks = list.reduce((sum, r) => sum + (r.breakCount || 0), 0)
  const h = Math.floor(totalMinutes / 60)
  const m = totalMinutes % 60
  return {
    workDays,
    totalHours,
    totalHoursText: `${h} ч ${String(m).padStart(2, '0')} мин`,
    breaks
  }
})

function onDownload() {
  const rows = [['Дата','Приход','Уход','Статус','Часы'], ...filteredReports.value.map(r => [r.date, r.startTime, r.endTime || '', r.statusLabel, r.totalMinutes])]
  const csv = '\ufeff' + rows.map(row => row.map(value => '"' + String(value).replaceAll('"', '""') + '"').join(';')).join('\r\n')
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
  const link = document.createElement('a')
  link.href = url
  link.download = `my-days-${localDate()}.csv`
  link.click()
  URL.revokeObjectURL(url)
}

watch(selectedPeriod, loadHistory)
onMounted(loadHistory)

const avatarLetters = computed(() => {
  const name = auth.fullName || auth.login || 'U'
  return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2)
})

const roleLabel = computed(() => {
  const roles = { 'Employee': 'Разработчик', 'Manager': 'Руководитель', 'Admin': 'Администратор' }
  return roles[auth.role] || 'Сотрудник'
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
  margin-bottom: 32px;
}

.page-header h1 {
  font-size: 28px;
  font-weight: 700;
  color: #1a1a1a;
  margin: 0;
}

.period-selector {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font-size: 14px;
  color: #1a1a1a;
  cursor: pointer;
}

.period-selector:hover {
  border-color: #2563eb;
}

.calendar-icon {
  font-size: 16px;
}

.chevron {
  font-size: 12px;
  color: #9ca3af;
}

.summary-cards {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 20px;
  margin-bottom: 32px;
}

.card {
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 24px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.card-title {
  font-size: 14px;
  color: #6b7280;
}

.card-icon {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
}

.icon-blue { background: #dbeafe; }
.icon-green { background: #d1fae5; }
.icon-orange { background: #fef3c7; }

.card-value {
  font-size: 36px;
  font-weight: 700;
}

.card-value.blue { color: #2563eb; }
.card-value.green { color: #10b981; }
.card-value.orange { color: #f59e0b; }

.reports-table-card {
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  overflow: hidden;
}

.table-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  border-bottom: 1px solid #e5e7eb;
}

.table-title {
  font-size: 16px;
  font-weight: 600;
  color: #1a1a1a;
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

.table-actions {
  display: flex;
  gap: 12px;
  align-items: center;
}

.filter-dropdown {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  font-size: 13px;
  color: #4b5563;
  cursor: pointer;
}

.filter-dropdown:hover {
  border-color: #2563eb;
}

.btn-download {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  font-size: 13px;
  color: #4b5563;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-download:hover {
  border-color: #2563eb;
  color: #2563eb;
}

.download-icon {
  font-size: 14px;
}

.reports-table {
  width: 100%;
  border-collapse: collapse;
}

.reports-table th {
  text-align: left;
  padding: 12px 24px;
  font-size: 12px;
  font-weight: 500;
  color: #6b7280;
  background: #f9fafb;
  border-bottom: 1px solid #e5e7eb;
}

.reports-table td {
  padding: 16px 24px;
  font-size: 14px;
  color: #1a1a1a;
  border-bottom: 1px solid #f3f4f6;
}

.date-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.date-main {
  font-weight: 500;
}

.date-weekday {
  font-size: 12px;
  color: #6b7280;
}

.time-cell {
  font-variant-numeric: tabular-nums;
  color: #4b5563;
}

.status-badge {
  display: inline-block;
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 500;
}

.status-badge.completed {
  background: #d1fae5;
  color: #065f46;
}

.status-badge.partial {
  background: #fef3c7;
  color: #92400e;
}

.status-badge.absent {
  background: #fee2e2;
  color: #991b1b;
}

.btn-view {
  display: inline-block;
  padding: 8px 16px;
  background: #2563eb;
  color: white;
  border-radius: 6px;
  font-size: 13px;
  text-decoration: none;
  transition: background 0.2s;
}

.btn-view:hover {
  background: #1d4ed8;
}

.table-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 24px;
  font-size: 13px;
  color: #6b7280;
  border-top: 1px solid #e5e7eb;
}

.footer-right strong {
  color: #1a1a1a;
  font-size: 14px;
}

.dropdown-menu {
  position: fixed;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  z-index: 100;
  min-width: 200px;
}

.period-menu {
  top: 100px;
  right: 40px;
}

.status-menu {
  top: 200px;
  right: 200px;
}

.dropdown-item {
  padding: 10px 16px;
  font-size: 14px;
  color: #4b5563;
  cursor: pointer;
  transition: background 0.2s;
}

.dropdown-item:hover {
  background: #f3f4f6;
}

.dropdown-item.active {
  color: #2563eb;
  font-weight: 500;
}
</style>