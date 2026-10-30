import { createRouter, createWebHistory, type RouteLocationNormalized } from 'vue-router'
import { useAuth } from '@/auth/useAuth'

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    admin?: boolean
    superAdmin?: boolean
  }
}

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'start', component: () => import('@/views/StartView.vue') },
    { path: '/locations', name: 'locations', component: () => import('@/views/LocationsView.vue') },
    {
      path: '/locations/:id',
      name: 'location',
      component: () => import('@/views/LocationView.vue'),
      props: true,
    },
    { path: '/my-shifts', name: 'my-shifts', component: () => import('@/views/MyShiftsView.vue') },
    {
      path: '/help',
      name: 'help',
      component: () => import('@/views/HelpView.vue'),
      meta: { public: true },
    },
    {
      path: '/auth/callback',
      name: 'auth-callback',
      component: () => import('@/views/auth/CallbackView.vue'),
      meta: { public: true },
    },
    {
      path: '/auth/silent',
      name: 'auth-silent',
      component: () => import('@/views/auth/SilentView.vue'),
      meta: { public: true },
    },
    {
      path: '/auth/logged-out',
      name: 'logged-out',
      component: () => import('@/views/auth/LoggedOutView.vue'),
      meta: { public: true },
    },
    { path: '/admin', redirect: '/admin/locations', meta: { admin: true } },
    {
      path: '/admin/seasons',
      name: 'admin-seasons',
      component: () => import('@/views/admin/SeasonsView.vue'),
      meta: { admin: true },
    },
    {
      path: '/admin/shift-types',
      name: 'admin-shift-types',
      component: () => import('@/views/admin/ShiftTypesView.vue'),
      meta: { admin: true },
    },
    {
      path: '/admin/frameworks',
      name: 'admin-frameworks',
      component: () => import('@/views/admin/FrameworksView.vue'),
      meta: { admin: true },
    },
    {
      path: '/admin/locations',
      name: 'admin-locations',
      component: () => import('@/views/admin/LocationsView.vue'),
      meta: { admin: true },
    },
    {
      path: '/admin/locations/:id/containers',
      name: 'admin-containers',
      component: () => import('@/views/admin/ContainersView.vue'),
      props: true,
      meta: { admin: true },
    },
    {
      path: '/admin/setup',
      name: 'admin-setup',
      component: () => import('@/views/admin/SetupWizardView.vue'),
      meta: { admin: true },
    },
    {
      path: '/admin/export',
      name: 'admin-export',
      component: () => import('@/views/admin/ExportView.vue'),
      meta: { admin: true },
    },
    {
      path: '/admin/settings',
      name: 'admin-settings',
      component: () => import('@/views/admin/SettingsView.vue'),
      meta: { admin: true, superAdmin: true },
    },
    {
      path: '/statistics',
      name: 'admin-statistics',
      component: () => import('@/views/admin/StatisticsView.vue'),
      meta: { admin: true },
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/views/NotFoundView.vue'),
      meta: { public: true },
    },
  ],
  scrollBehavior: (_to, _from, saved) => saved ?? { top: 0 },
})

router.beforeEach(async (to: RouteLocationNormalized) => {
  if (to.meta.public) return true
  const auth = useAuth()
  if (!auth.isAuthenticated.value) {
    await auth.login(to.fullPath)
    return false
  }
  if (to.meta.superAdmin && !auth.isSuperAdmin.value)
    return { name: 'start', query: { denied: '1' } }
  if (to.meta.admin && !auth.isAdmin.value) return { name: 'start', query: { denied: '1' } }
  return true
})
