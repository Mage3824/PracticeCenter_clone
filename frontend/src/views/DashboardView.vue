<template>
  <div class="page">
    <header class="header">
      <div class="header-left">
        <span class="logo">Программный центр</span>
        <nav class="nav">
          <router-link to="/dashboard" class="nav-link" active-class="active">
            Мой день
          </router-link>
          <router-link to="/reports" class="nav-link" active-class="active">
            Мои отчёты
          </router-link>
          <router-link v-if="auth.role !== 'Employee'" to="/manager" class="nav-link" active-class="active">
            Дашборд руководителя
          </router-link>
          <router-link v-if="auth.role !== 'Employee'" to="/team" class="nav-link" active-class="active">
            Команда
          </router-link>
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
        <button class="btn btn-cancel" @click="auth.logout(); router.push('/')">Выйти</button>
      </div>
    </header>

    <main class="main-content">
      <div class="page-header">
        <h1>Мой рабочий день</h1>
        <div class="current-date">{{ formattedDate }}</div>
      </div>

      <p v-if="apiError" role="alert" style="color:#b91c1c; margin-bottom:12px">{{ apiError }}</p>
      <p v-if="busy" style="color:#64748b">Синхронизация с сервером…</p>
      <div class="timer-banner" :class="{ active: workDayStarted, finished: workDayFinished, onBreak: isOnBreak }">
        <div class="banner-left">
          <div class="banner-title">
            {{ bannerTitle }}
          </div>
          <div class="banner-subtitle">
            {{ bannerSubtitle }}
          </div>

          <div v-if="isOnBreak" class="break-timer-display">
            ☕ Перерыв: {{ currentBreakDuration }}
          </div>
        </div>
        <div class="banner-right">
          <div class="timer-label">Рабочее время сегодня</div>
          <div class="timer-value">{{ timerDisplay }}</div>
          <div class="timer-remaining">
            До нормы 8 ч осталось {{ remainingTime }}
          </div>
        </div>
      </div>

      <div class="action-buttons">
        <button
          class="btn btn-start"
          :disabled="busy || workDayStarted || workDayFinished"
          @click="onStartDay"
        >
          {{ workDayStarted ? 'День начат' : 'Начать день' }}
        </button>

        <button
          class="btn btn-break"
          :class="{ active: isOnBreak }"
          :disabled="busy || !workDayStarted || workDayFinished"
          @click="onToggleBreak"
        >
          {{ isOnBreak ? `Завершить перерыв (${currentBreakDuration})` : 'Начать перерыв' }}
        </button>

        <button
          class="btn btn-finish"
          :disabled="busy || !workDayStarted || workDayFinished"
          @click="onFinishDay"
        >
          Завершить день
        </button>
      </div>

      <div class="two-columns">
        <div class="report-card">
          <div class="report-header">
            <h2>Отчёт за день</h2>
            <span class="report-status">{{ reportStatus }}</span>
          </div>

          <textarea
            v-model="reportText"
            class="report-textarea"
            placeholder="Опишите, что вы сделали сегодня..."
            :disabled="workDayFinished && reportSubmitted"
          ></textarea>

          <div class="report-footer">
            <button
              v-if="!workDayFinished"
              class="btn btn-save-draft"
              :disabled="!reportText || !workDayStarted"
              @click="onSaveDraft"
            >
              Сохранить черновик
            </button>
            <button
              v-if="workDayFinished && !reportSubmitted"
              class="btn btn-save-draft"
              :disabled="busy || !reportText.trim()"
              @click="onSubmitAfterFinish"
            >
              Отправить отчёт на сервер
            </button>
            <div v-if="lastSavedAt" class="save-status">
              ✓ Черновик сохранён в браузере в {{ lastSavedAt }}
            </div>
          </div>
        </div>

        <div class="summary-card">
          <h2>Сводка часов</h2>

          <div class="summary-main">
            <div class="summary-total">{{ totalHoursText }}</div>
            <div class="summary-norm">из 8 ч</div>
          </div>

          <div class="progress-bar">
            <div
              class="progress-fill"
              :style="{ width: progressPercent + '%' }"
            ></div>
          </div>

          <div class="summary-details">
            <div class="summary-row">
              <span>Начало дня</span>
              <span class="value">{{ startTimeText }}</span>
            </div>
            <div class="summary-row">
              <span>Текущий перерыв</span>
              <span class="value" :class="{ highlight: isOnBreak }">
                {{ isOnBreak ? currentBreakDuration : '—' }}
              </span>
            </div>
            <div class="summary-row">
              <span>Всего перерывов</span>
              <span class="value">{{ breakMinutesText }}</span>
            </div>
            <div class="summary-row">
              <span>Осталось до нормы</span>
              <span class="value">{{ remainingTime }}</span>
            </div>
          </div>

          <div class="summary-weekly">
            <div class="weekly-label">За эту неделю</div>
            <div class="weekly-value">{{ weekHoursText }}</div>
            <router-link to="/reports" class="weekly-link">
              Открыть мои отчёты →
            </router-link>
          </div>
        </div>
      </div>
    </main>

    <div v-if="showConfirmModal" class="modal-overlay" @click.self="showConfirmModal = false">
      <div class="modal">
        <h3>{{ confirmTitle }}</h3>
        <p>{{ confirmMessage }}</p>
        <div class="modal-actions">
          <button class="btn btn-cancel" @click="showConfirmModal = false">
            Отмена
          </button>
          <button class="btn btn-confirm" @click="confirmAction">
            Подтвердить
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth.js'
import { localDate, getCurrentDay, getHistory, listBreaks, startDay, finishDay, startBreak, finishBreak, submitReport, searchReports } from '@/api/worktime.js'

const router = useRouter()
const auth = useAuthStore()
const apiError = ref('')
const busy = ref(false)
const activeDay = ref(null)
const currentBreak = ref(null)
const weekMinutes = ref(0)
const weekHoursText = computed(() => `${Math.floor(weekMinutes.value / 60)} ч ${weekMinutes.value % 60} мин (до вычета перерывов)`)

const workDayStarted = ref(false)
const workDayFinished = ref(false)
const isOnBreak = ref(false)
const startTime = ref(null)
const breakStartTime = ref(null)
const totalBreakMinutes = ref(0)
const reportText = ref('')
const reportSubmitted = ref(false)
const lastSavedAt = ref('')

const now = ref(Date.now())
let timer = null

function startTimer() {
  if (timer) return
  timer = setInterval(() => {
    now.value = Date.now()
  }, 1000)
}

function stopTimer() {
  if (timer) {
    clearInterval(timer)
    timer = null
  }
}

const avatarLetters = computed(() => {
  const name = auth.fullName || auth.login || 'U'
  return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2)
})

const roleLabel = computed(() => {
  const roles = {
    'Employee': 'Разработчик',
    'Manager': 'Руководитель',
    'Admin': 'Администратор'
  }
  return roles[auth.role] || 'Сотрудник'
})

const formattedDate = computed(() => {
  const days = ['Воскресенье', 'Понедельник', 'Вторник', 'Среда', 'Четверг', 'Пятница', 'Суббота']
  const months = ['января', 'февраля', 'марта', 'апреля', 'мая', 'июня', 'июля', 'августа', 'сентября', 'октября', 'ноября', 'декабря']
  const d = new Date()
  return `${days[d.getDay()]}, ${d.getDate()} ${months[d.getMonth()]} ${d.getFullYear()}`
})

const bannerTitle = computed(() => {
  if (workDayFinished.value) return 'Рабочий день завершён'
  if (isOnBreak.value) return 'На перерыве'
  if (workDayStarted.value) return 'Вы в рабочем ритме'
  return 'Рабочий день не начат'
})

const bannerSubtitle = computed(() => {
  if (!workDayStarted.value) return 'Нажмите "Начать день", чтобы зафиксировать приход'
  const parts = []
  if (startTime.value) {
    parts.push(`Начало в ${formatTime(startTime.value)}`)
  }
  if (totalBreakMinutes.value > 0) {
    parts.push(`Перерывы: ${totalBreakMinutes.value} мин`)
  }
  return parts.join(' · ') || ''
})

const currentBreakDuration = computed(() => {
  if (!isOnBreak.value || !breakStartTime.value) return '0 мин'
  const breakMs = now.value - breakStartTime.value.getTime()
  const breakMinutes = Math.floor(breakMs / 60000)
  const breakSeconds = Math.floor((breakMs % 60000) / 1000)
  if (breakMinutes > 0) {
    return `${breakMinutes} мин ${breakSeconds} сек`
  }
  return `${breakSeconds} сек`
})

const totalBreakMs = computed(() => {
  const completedBreaksMs = totalBreakMinutes.value * 60 * 1000
  let currentBreakMs = 0
  if (isOnBreak.value && breakStartTime.value) {
    currentBreakMs = now.value - breakStartTime.value.getTime()
  }
  return completedBreaksMs + currentBreakMs
})

const timerDisplay = computed(() => {
  if (!workDayStarted.value || !startTime.value) return '00:00:00'
  const workedMs = now.value - startTime.value.getTime() - totalBreakMs.value
  return formatDuration(Math.max(0, workedMs))
})

const remainingTime = computed(() => {
  if (!workDayStarted.value || !startTime.value) return '8 ч 0 мин'
  const workedMs = now.value - startTime.value.getTime() - totalBreakMs.value
  const workedMinutes = Math.floor(Math.max(0, workedMs) / 60000)
  const remaining = 480 - workedMinutes
  if (remaining <= 0) return '0 мин'
  const h = Math.floor(remaining / 60)
  const m = remaining % 60
  if (h > 0) return `${h} ч ${m} мин`
  return `${m} мин`
})

const totalHoursText = computed(() => {
  if (!workDayStarted.value || !startTime.value) return '0 мин'
  const workedMs = now.value - startTime.value.getTime() - totalBreakMs.value
  const workedMinutes = Math.floor(Math.max(0, workedMs) / 60000)
  const h = Math.floor(workedMinutes / 60)
  const m = workedMinutes % 60
  if (h > 0) return `${h} ч ${m} мин`
  return `${m} мин`
})

const progressPercent = computed(() => {
  if (!workDayStarted.value || !startTime.value) return 0
  const workedMs = now.value - startTime.value.getTime() - totalBreakMs.value
  const workedMinutes = Math.floor(Math.max(0, workedMs) / 60000)
  return Math.min(100, (workedMinutes / 480) * 100)
})

const startTimeText = computed(() => {
  return startTime.value ? formatTime(startTime.value) : '—'
})

const breakMinutesText = computed(() => {
  return totalBreakMinutes.value > 0 ? `${totalBreakMinutes.value} мин` : '0 мин'
})

const reportStatus = computed(() => {
  if (!workDayStarted.value) return 'Не начат'
  if (workDayFinished.value) return reportSubmitted.value ? 'Отправлен' : 'Не отправлен'
  if (reportText.value) return 'Черновик'
  return 'Пусто'
})

const showConfirmModal = ref(false)
const confirmTitle = ref('')
const confirmMessage = ref('')
let pendingAction = null

function askConfirm(title, message, action) {
  confirmTitle.value = title
  confirmMessage.value = message
  pendingAction = action
  showConfirmModal.value = true
}

function confirmAction() {
  if (pendingAction) pendingAction()
  showConfirmModal.value = false
}

const draftKey = () => `worktime-draft-${auth.userId}-${localDate()}`

// Restore server state after reload; never assume the local timer is authoritative.
async function refreshDay() {
  busy.value = true
  apiError.value = ''
  try {
    const today = localDate()
    const day = await getCurrentDay(today)
    const days = await getHistory(today, today)
    const latest = day || days[0] || null
    activeDay.value = latest
    workDayStarted.value = !!latest
    workDayFinished.value = !!latest && latest.status !== 'Active'
    isOnBreak.value = false
    currentBreak.value = null
    breakStartTime.value = null
    totalBreakMinutes.value = 0
    if (latest) {
      startTime.value = new Date(`${today}T${latest.startTime}:00`)
      const breaks = await listBreaks(latest.id)
      totalBreakMinutes.value = breaks.filter(b => b.endTime && b.countedInReports)
        .reduce((sum, b) => sum + (b.durationMinutes || 0), 0)
      const openBreak = breaks.find(b => !b.endTime)
      if (openBreak) {
        currentBreak.value = openBreak
        isOnBreak.value = true
        breakStartTime.value = new Date(`${today}T${openBreak.startTime}:00`)
      }
      now.value = workDayFinished.value && latest.endTime
        ? new Date(`${today}T${latest.endTime}:00`).getTime() : Date.now()
      if (workDayFinished.value) stopTimer()
      else startTimer()
    } else {
      stopTimer()
      startTime.value = null
      now.value = Date.now()
    }
    reportText.value = localStorage.getItem(draftKey()) || ''
    reportSubmitted.value = false
    if (latest && latest.status !== 'Active') {
      const found = await searchReports({ employeeId: auth.userId, from: today, to: today })
      const submitted = found.find(r => r.workDayId === latest.id)
      reportSubmitted.value = !!submitted
      if (submitted) reportText.value = submitted.text
    }
    const monday = new Date()
    monday.setDate(monday.getDate() - ((monday.getDay() + 6) % 7))
    const weekDays = await getHistory(localDate(monday), today)
    weekMinutes.value = weekDays.filter(d => d.endTime && d.countedInReports).reduce((sum, d) => {
      const [sh, sm] = d.startTime.split(':').map(Number)
      const [eh, em] = d.endTime.split(':').map(Number)
      return sum + Math.max(0, (eh * 60 + em) - (sh * 60 + sm))
    }, 0)
  } catch (err) {
    apiError.value = err.message || 'Ошибка получения рабочего дня.'
  } finally { busy.value = false }
}

async function onStartDay() {
  busy.value = true
  apiError.value = ''
  try {
    const day = await startDay()
    activeDay.value = day
    workDayStarted.value = true
    workDayFinished.value = false
    startTime.value = new Date(`${day.date}T${day.startTime}:00`)
    now.value = Date.now()
    startTimer()
  } catch (err) { apiError.value = err.message }
  finally { busy.value = false }
}

async function onToggleBreak() {
  if (!activeDay.value) return
  busy.value = true
  apiError.value = ''
  try {
    if (!isOnBreak.value) {
      currentBreak.value = await startBreak(activeDay.value.id)
      breakStartTime.value = new Date(`${localDate()}T${currentBreak.value.startTime}:00`)
      isOnBreak.value = true
    } else {
      const closed = await finishBreak(currentBreak.value.id)
      if (closed.countedInReports) totalBreakMinutes.value += closed.durationMinutes || 0
      isOnBreak.value = false
      breakStartTime.value = null
      currentBreak.value = null
    }
    now.value = Date.now()
  } catch (err) { apiError.value = err.message }
  finally { busy.value = false }
}

function onFinishDay() {
  askConfirm('Завершить рабочий день?',
    'После завершения смены отчёт будет отправлен на сервер и станет недоступен для редактирования.',
    async () => {
      if (!activeDay.value) return
      busy.value = true
      apiError.value = ''
      try {
        // Keep the text for retry if the report request fails after the day is closed.
        if (reportText.value.trim()) localStorage.setItem(draftKey(), reportText.value)
        const finished = await finishDay(activeDay.value.id)
        activeDay.value = finished
        workDayFinished.value = true
        isOnBreak.value = false
        currentBreak.value = null
        breakStartTime.value = null
        now.value = new Date(`${localDate()}T${finished.endTime}:00`).getTime()
        stopTimer()
        if (reportText.value.trim()) {
          try {
            await submitReport(finished.id, reportText.value.trim())
            reportSubmitted.value = true
            localStorage.removeItem(draftKey())
            lastSavedAt.value = 'на сервере'
          } catch (err) {
            apiError.value = `Смена завершена, но отправка отчёта не удалась: ${err.message}`
          }
        }
      } catch (err) { apiError.value = err.message }
      finally { busy.value = false }
    }
  )
}

async function onSubmitAfterFinish() {
  if (!activeDay.value || !workDayFinished.value || reportSubmitted.value || !reportText.value.trim()) return
  busy.value = true
  apiError.value = ''
  try {
    await submitReport(activeDay.value.id, reportText.value.trim())
    reportSubmitted.value = true
    localStorage.removeItem(draftKey())
    lastSavedAt.value = 'на сервере'
  } catch (err) {
    apiError.value = `Не удалось отправить отчёт: ${err.message}`
  } finally {
    busy.value = false
  }
}

function onSaveDraft() {
  localStorage.setItem(draftKey(), reportText.value)
  const date = new Date()
  lastSavedAt.value = formatTime(date)
}

onMounted(refreshDay)

function formatTime(date) {
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}

function formatDuration(ms) {
  const totalSeconds = Math.floor(ms / 1000)
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  const s = totalSeconds % 60
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
}

onUnmounted(() => {
  stopTimer()
})
</script>

<style scoped>
.page {
  min-height: 100vh;
  background: #f5f7fa;
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

.timer-banner {
  background: linear-gradient(135deg, #3b82f6 0%, #60a5fa 100%);
  border-radius: 12px;
  padding: 32px 40px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  color: white;
  margin-bottom: 24px;
  transition: background 0.3s;
}

.timer-banner.finished {
  background: linear-gradient(135deg, #6b7280 0%, #9ca3af 100%);
}

.timer-banner.onBreak {
  background: linear-gradient(135deg, #f59e0b 0%, #fbbf24 100%);
}

.banner-left {
  flex: 1;
}

.banner-title {
  font-size: 24px;
  font-weight: 600;
  margin-bottom: 8px;
}

.banner-subtitle {
  font-size: 14px;
  opacity: 0.9;
}

.break-timer-display {
  margin-top: 12px;
  padding: 8px 16px;
  background: rgba(255, 255, 255, 0.25);
  border-radius: 6px;
  font-size: 16px;
  font-weight: 600;
  display: inline-block;
}

.banner-right {
  text-align: right;
}

.timer-label {
  font-size: 13px;
  opacity: 0.9;
  margin-bottom: 4px;
}

.timer-value {
  font-size: 48px;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  line-height: 1;
  margin-bottom: 4px;
}

.timer-remaining {
  font-size: 13px;
  opacity: 0.9;
}

.action-buttons {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.btn {
  padding: 14px 24px;
  border: none;
  border-radius: 8px;
  font-size: 15px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-start {
  background: #10b981;
  color: white;
}

.btn-start:hover:not(:disabled) {
  background: #059669;
}

.btn-break {
  background: #f59e0b;
  color: white;
}

.btn-break:hover:not(:disabled) {
  background: #d97706;
}

.btn-break.active {
  background: #d97706;
  box-shadow: 0 0 0 3px rgba(245, 158, 11, 0.4);
}

.btn-finish {
  background: #ef4444;
  color: white;
}

.btn-finish:hover:not(:disabled) {
  background: #dc2626;
}

.two-columns {
  display: grid;
  grid-template-columns: 1.5fr 1fr;
  gap: 24px;
}

.report-card {
  background: white;
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
}

.report-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.report-header h2 {
  font-size: 18px;
  font-weight: 600;
  color: #1a1a1a;
  margin: 0;
}

.report-status {
  font-size: 13px;
  color: #6b7280;
  background: #f3f4f6;
  padding: 4px 12px;
  border-radius: 12px;
}

.report-textarea {
  width: 100%;
  min-height: 200px;
  padding: 16px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  font-size: 14px;
  font-family: inherit;
  resize: vertical;
  box-sizing: border-box;
  transition: border-color 0.2s;
}

.report-textarea:focus {
  outline: none;
  border-color: #2563eb;
}

.report-textarea:disabled {
  background: #f9fafb;
  cursor: not-allowed;
}

.report-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 16px;
}

.btn-save-draft {
  background: #2563eb;
  color: white;
  padding: 10px 20px;
  border-radius: 6px;
  font-size: 14px;
}

.btn-save-draft:hover:not(:disabled) {
  background: #1d4ed8;
}

.save-status {
  font-size: 13px;
  color: #10b981;
}

.summary-card {
  background: white;
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
}

.summary-card h2 {
  font-size: 18px;
  font-weight: 600;
  color: #1a1a1a;
  margin: 0 0 20px;
}

.summary-main {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 16px;
}

.summary-total {
  font-size: 32px;
  font-weight: 700;
  color: #1a1a1a;
}

.summary-norm {
  font-size: 14px;
  color: #6b7280;
}

.progress-bar {
  height: 8px;
  background: #e5e7eb;
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 20px;
}

.progress-fill {
  height: 100%;
  background: #2563eb;
  border-radius: 4px;
  transition: width 1s ease;
}

.summary-details {
  margin-bottom: 24px;
}

.summary-row {
  display: flex;
  justify-content: space-between;
  padding: 10px 0;
  border-bottom: 1px solid #f3f4f6;
  font-size: 14px;
  color: #4b5563;
}

.summary-row .value {
  font-weight: 500;
  color: #1a1a1a;
}

.summary-row .value.highlight {
  color: #f59e0b;
  font-weight: 700;
}

.summary-weekly {
  padding-top: 16px;
  border-top: 1px solid #e5e7eb;
}

.weekly-label {
  font-size: 13px;
  color: #6b7280;
  margin-bottom: 4px;
}

.weekly-value {
  font-size: 14px;
  font-weight: 500;
  color: #1a1a1a;
  margin-bottom: 8px;
}

.weekly-link {
  font-size: 13px;
  color: #2563eb;
  text-decoration: none;
}

.weekly-link:hover {
  text-decoration: underline;
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
  max-width: 400px;
  width: 90%;
}

.modal h3 {
  margin: 0 0 12px;
  font-size: 18px;
  color: #1a1a1a;
}

.modal p {
  margin: 0 0 24px;
  color: #4b5563;
  font-size: 14px;
  line-height: 1.5;
}

.modal-actions {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
}

.btn-cancel {
  background: #f3f4f6;
  color: #4b5563;
}

.btn-cancel:hover {
  background: #e5e7eb;
}

.btn-confirm {
  background: #ef4444;
  color: white;
}

.btn-confirm:hover {
  background: #dc2626;
}
</style>