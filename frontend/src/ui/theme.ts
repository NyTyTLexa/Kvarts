import { useEffect, useState } from 'react'

export const THEMES = [
  { id: 'cabinet', label: 'Кабинет', hint: 'синий рабочий стол', swatch: '#1b6aa3' },
  { id: 'graphite', label: 'Графит', hint: 'нейтральный', swatch: '#4a5c6c' },
  { id: 'forest', label: 'Склад', hint: 'зелёный учёт', swatch: '#2d6a4f' },
  { id: 'night', label: 'Ночь', hint: 'тёмная смена', swatch: '#4aa3d4' },
] as const

export type ThemeId = (typeof THEMES)[number]['id']

const KEY = 'ps-theme'
export const THEME_EVENT = 'ps-theme'

export function isThemeId(v: string): v is ThemeId {
  return THEMES.some(t => t.id === v)
}

export function currentTheme(): ThemeId {
  const raw = localStorage.getItem(KEY) ?? ''
  return isThemeId(raw) ? raw : 'cabinet'
}

const THEME_BAR: Record<ThemeId, string> = {
  cabinet: '#243447',
  graphite: '#2a323a',
  forest: '#1d3d32',
  night: '#0c141c',
}

export function applyTheme(id: ThemeId) {
  document.documentElement.setAttribute('data-theme', id)
  document.documentElement.style.colorScheme = id === 'night' ? 'dark' : 'light'
  localStorage.setItem(KEY, id)
  const meta = document.querySelector('meta[name="theme-color"]')
  if (meta) meta.setAttribute('content', THEME_BAR[id])
  window.dispatchEvent(new CustomEvent(THEME_EVENT, { detail: id }))
}

export function bootTheme() {
  applyTheme(currentTheme())
}

export function useTheme() {
  const [id, setId] = useState<ThemeId>(() => currentTheme())
  useEffect(() => {
    const on = (e: Event) => setId((e as CustomEvent<ThemeId>).detail)
    window.addEventListener(THEME_EVENT, on)
    return () => window.removeEventListener(THEME_EVENT, on)
  }, [])
  return [id, applyTheme] as const
}
