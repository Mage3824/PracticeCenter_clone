import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth.js'

import LoginView from '@/views/LoginView.vue'
import DashboardView from '@/views/DashboardView.vue'
import ReportsView from '@/views/ReportsView.vue'
import DailyReportView from '@/views/DailyReportView.vue'
import ManagerView from '@/views/ManagerView.vue'
import TeamView from '@/views/TeamView.vue'
import UsersView from '@/views/UsersView.vue'
import SystemView from '@/views/SystemView.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'login',
      component: LoginView
    },
    {
      path: '/dashboard',
      name: 'dashboard',
      component: DashboardView,
      meta: { requiresAuth: true }
    },
    {
      path: '/reports',
      name: 'reports',
      component: ReportsView,
      meta: { requiresAuth: true }
    },
    {
      path: '/reports/:id',
      name: 'daily-report',
      component: DailyReportView,
      meta: { requiresAuth: true }
    },
    {
      path: '/manager',
      name: 'manager',
      component: ManagerView,
      meta: { requiresAuth: true, managerOnly: true }
    },
    {
      path: '/team',
      name: 'team',
      component: TeamView,
      meta: { requiresAuth: true, managerOnly: true }
    },
    {
      path: '/users',     
      name: 'users',
      component: UsersView,
      meta: { requiresAuth: true, requiredRole: 'Admin' }
    },
    {
      path: '/system',
      name: 'system',
      component: SystemView,
      meta: { requiresAuth: true, requiredRole: 'Admin' }
    }
  ]
})

router.beforeEach((to, from, next) => {
  const auth = useAuthStore()
  if (to.meta.requiresAuth && !auth.isLoggedIn) {
    next('/')
  } else if (to.meta.requiredRole && auth.role !== to.meta.requiredRole) {
    next('/dashboard')
  } else if (to.meta.managerOnly && !['Admin', 'Manager'].includes(auth.role)) {
    next('/dashboard')
  } else if (to.path === '/' && auth.isLoggedIn) {
    next('/dashboard')
  } else {
    next()
  }
})

export default router