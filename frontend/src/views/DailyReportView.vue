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
      <button class="btn-back" @click="$router.back()">← Назад к отчётам</button>

      <p v-if="apiError" role="alert" style="color:#b91c1c">{{ apiError }}</p>
      <div class="report-header">
        <h1>Отчёт за {{ report.dateFormatted }}</h1>
        <span class="status-badge" :class="report.statusClass">{{ report.statusLabel }}</span>
      </div>

      <div class="summary-cards">
        <div class="card">
          <div class="card-title">Приход</div>
          <div class="card-value">{{ report.startTime || '—' }}</div>
        </div>
        <div class="card">
          <div class="card-title">Уход</div>
          <div class="card-value">{{ report.endTime || '—' }}</div>
        </div>
        <div class="card">
          <div class="card-title">Перерывы</div>
          <div class="card-value">{{ report.breaks.length }} шт.</div>
        </div>
        <div class="card">
          <div class="card-title">Отработано</div>
          <div class="card-value highlight">{{ report.workedHoursText }}</div>
        </div>
      </div>

      <div v-if="report.breaks.length > 0" class="breaks-card">
        <h2>Перерывы</h2>
        <div class="breaks-list">
          <div v-for="(b, i) in report.breaks" :key="i" class="break-item">
            <div class="break-time">{{ b.startTime }} — {{ b.endTime || 'открыт' }}</div>
            <div class="break-duration">{{ breakDurationText(b) }}</div>
          </div>
        </div>
      </div>

      <div class="report-text-card">
        <h2>Описание работы</h2>
        <p class="report-text">{{ report.text || 'Описание не добавлено' }}</p>
      </div>
    </main>
  </div>
</template>

<script setup>
import { computed, ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth.js'
import { getDaily, getHistory, searchReports, localDate } from '@/api/worktime.js'

const route = useRoute()
const auth = useAuthStore()

const dayNames = ['Воскресенье', 'Понедельник', 'Вторник', 'Среда', 'Четверг', 'Пятница', 'Суббота']
const monthNames = ['января', 'февраля', 'марта', 'апреля', 'мая', 'июня', 'июля', 'августа', 'сентября', 'октября', 'ноября', 'декабря']

const apiError = ref('')
const rawReport = ref({ date: localDate(), startTime: '', endTime: '', status: 'Active', totalMinutes: 0, text: '', breaks: [] })
async function loadDay() {
  try {
    const today = new Date()
    const from = new Date(today)
    from.setDate(from.getDate() - 365)
    const days = await getHistory(localDate(from), localDate(today))
    const day = days.find(d => d.id === route.params.id)
    if (!day) throw new Error('Рабочий день не найден в истории за последний год.')
    const [details, entries] = await Promise.all([
      getDaily(day.id),
      searchReports({ employeeId: auth.userId, from: day.date, to: day.date })
    ])
    rawReport.value = { ...day, ...details, text: entries.find(r => r.workDayId === day.id)?.text || '',
      breaks: details?.breaks || [], totalMinutes: details?.totalMinutes || 0 }
  } catch (error) { apiError.value = error.message || 'Не удалось загрузить отчёт.' }
}
onMounted(loadDay)

const statusMap = {
  Finished: { label: 'Отработано', class: 'completed' },
  Active: { label: 'Не завершён', class: 'partial' },
  AutoFinished: { label: 'Автозавершение', class: 'partial' }
}

const report = computed(() => {
  const d = new Date(rawReport.value.date)
  const h = Math.floor(rawReport.value.totalMinutes / 60)
  const m = rawReport.value.totalMinutes % 60
  return {
    ...rawReport.value,
    dateFormatted: `${d.getDate()} ${monthNames[d.getMonth()]} ${d.getFullYear()} (${dayNames[d.getDay()]})`,
    workedHoursText: `${h} ч ${m} мин`,
    statusLabel: statusMap[rawReport.value.status]?.label || rawReport.value.status,
    statusClass: statusMap[rawReport.value.status]?.class || ''
  }
})

function breakDurationText(b) {
  if (!b.endTime) return 'открыт'
  const [sh, sm] = b.startTime.split(':').map(Number)
  const [eh, em] = b.endTime.split(':').map(Number)
  const diff = (eh * 60 + em) - (sh * 60 + sm)
  return `${diff} мин`
}

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
}

.nav-link.active {
  color: #2563eb;
  font-weight: 500;
  border-bottom: 2px solid #2563eb;
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
  max-width: 900px;
  margin: 0 auto;
  padding: 32px 40px;
}

.btn-back {
  background: none;
  border: none;
  color: #2563eb;
  font-size: 14px;
  cursor: pointer;
  padding: 0;
  margin-bottom: 24px;
}

.btn-back:hover {
  text-decoration: underline;
}

.report-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 32px;
}

.report-header h1 {
  font-size: 28px;
  font-weight: 700;
  color: #1a1a1a;
  margin: 0;
}

.status-badge {
  padding: 6px 16px;
  border-radius: 12px;
  font-size: 13px;
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

.summary-cards {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.card {
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 20px;
}

.card-title {
  font-size: 13px;
  color: #6b7280;
  margin-bottom: 8px;
}

.card-value {
  font-size: 24px;
  font-weight: 700;
  color: #1a1a1a;
}

.card-value.highlight {
  color: #2563eb;
}

.breaks-card,
.report-text-card {
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 24px;
  margin-bottom: 24px;
}

.breaks-card h2,
.report-text-card h2 {
  font-size: 16px;
  font-weight: 600;
  color: #1a1a1a;
  margin: 0 0 16px;
}

.breaks-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.break-item {
  display: flex;
  justify-content: space-between;
  padding: 12px 16px;
  background: #f9fafb;
  border-radius: 8px;
}

.break-time {
  font-weight: 500;
  color: #1a1a1a;
}

.break-duration {
  color: #6b7280;
}

.report-text {
  font-size: 14px;
  line-height: 1.6;
  color: #4b5563;
  margin: 0;
}
</style>