import { forwardRef, useCallback, useEffect, useImperativeHandle, useRef, useState } from 'react'
import { AlertTriangle, Captions, Keyboard, SkipForward, X } from 'lucide-react'
import type { LessonContent } from '../../types/dashboard'

const RATES = [0.75, 1, 1.25, 1.5, 1.75, 2] as const
const RATE_KEY = 'eduviet-playback-rate'
const AUTONEXT_KEY = 'eduviet-autonext'
const CAPTIONS_KEY = 'eduviet-captions'
const COUNTDOWN = 5

function readPref<T>(key: string, fallback: T, parse: (v: string) => T | null): T {
  try {
    const v = localStorage.getItem(key)
    return v === null ? fallback : parse(v) ?? fallback
  } catch {
    return fallback
  }
}
function writePref(key: string, value: string) {
  try { localStorage.setItem(key, value) } catch { /* trình duyệt chặn storage: chỉ nhớ trong phiên */ }
}

const shortcuts: [string, string][] = [
  ['Space hoặc K', 'Phát / tạm dừng'],
  ['← / J', 'Lùi 10 giây'],
  ['→ / L', 'Tới 10 giây'],
  ['Shift + > / <', 'Tăng / giảm tốc độ'],
  ['M', 'Tắt / bật tiếng'],
  ['C', 'Bật / tắt phụ đề'],
  ['F', 'Toàn màn hình'],
]

/** Không bắt phím tắt khi người dùng đang gõ hoặc thao tác nút, liên kết, ô chọn. */
function isTypingTarget(t: EventTarget | null) {
  const el = t as HTMLElement | null
  if (!el || el.tagName === 'VIDEO') return false
  return !!el.closest('input, textarea, select, button, a, [contenteditable="true"], [role="dialog"]')
}

export interface VideoPlayerHandle { video: HTMLVideoElement | null }

interface Props {
  lesson: LessonContent
  /** Hết video: đánh dấu hoàn thành (nếu được) */
  onEnded: () => void
  /** Chuyển sang bài tiếp theo (tự động sau đếm ngược hoặc bấm "Phát ngay") */
  onNext: () => void
  onReload: () => void
}

/**
 * Trình phát bài giảng: giữ điều khiển gốc của trình duyệt (dễ tiếp cận), thêm thanh công cụ tốc độ được nhớ lại,
 * phím tắt, phụ đề WebVTT và tự chuyển bài sau 5 giây.
 */
export const VideoPlayer = forwardRef<VideoPlayerHandle, Props>(function VideoPlayer({ lesson, onEnded, onNext, onReload }, ref) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const wrapRef = useRef<HTMLDivElement>(null)
  useImperativeHandle(ref, () => ({ get video() { return videoRef.current } }), [])

  const [rate, setRate] = useState<number>(() => readPref(RATE_KEY, 1, (v) => (RATES as readonly number[]).includes(Number(v)) ? Number(v) : null))
  const [autoNext, setAutoNext] = useState<boolean>(() => readPref(AUTONEXT_KEY, true, (v) => v === '1'))
  const [captions, setCaptions] = useState<boolean>(() => readPref(CAPTIONS_KEY, true, (v) => v === '1'))
  const [videoError, setVideoError] = useState(false)
  const [countdown, setCountdown] = useState<number | null>(null)
  const [flash, setFlash] = useState<string | null>(null)
  const [helpOpen, setHelpOpen] = useState(false)
  const flashTimer = useRef<number | undefined>(undefined)

  const showFlash = useCallback((text: string) => {
    setFlash(text)
    window.clearTimeout(flashTimer.current)
    flashTimer.current = window.setTimeout(() => setFlash(null), 900)
  }, [])

  // Bài mới: xóa lỗi và đếm ngược
  useEffect(() => {
    setVideoError(false)
    setCountdown(null)
  }, [lesson.id])

  useEffect(() => {
    if (videoRef.current) videoRef.current.playbackRate = rate
    writePref(RATE_KEY, String(rate))
  }, [rate, lesson.id])

  useEffect(() => writePref(AUTONEXT_KEY, autoNext ? '1' : '0'), [autoNext])

  // Bật/tắt phụ đề qua textTracks (track gốc của trình duyệt)
  const applyCaptions = useCallback((on: boolean) => {
    const track = videoRef.current?.textTracks?.[0]
    if (track) track.mode = on ? 'showing' : 'hidden'
  }, [])
  useEffect(() => {
    applyCaptions(captions)
    writePref(CAPTIONS_KEY, captions ? '1' : '0')
  }, [captions, applyCaptions, lesson.id])

  // Đếm ngược tự chuyển bài
  useEffect(() => {
    if (countdown === null) return
    if (countdown <= 0) {
      setCountdown(null)
      onNext()
      return
    }
    const id = window.setTimeout(() => setCountdown((c) => (c === null ? null : c - 1)), 1000)
    return () => window.clearTimeout(id)
  }, [countdown, onNext])

  const changeRate = useCallback((dir: 1 | -1) => {
    setRate((r) => {
      const i = RATES.indexOf(r as (typeof RATES)[number])
      const next = RATES[Math.min(RATES.length - 1, Math.max(0, (i < 0 ? 1 : i) + dir))]
      showFlash(`${String(next).replace('.', ',')}x`)
      return next
    })
  }, [showFlash])

  // Phím tắt toàn trang (trừ khi đang gõ chữ)
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const v = videoRef.current
      if (!v || e.ctrlKey || e.metaKey || e.altKey || isTypingTarget(e.target)) return
      const key = e.key.toLowerCase()
      const seekBy = (s: number) => {
        v.currentTime = Math.max(0, Math.min(v.duration || Infinity, v.currentTime + s))
        showFlash(s > 0 ? `+${s} giây` : `${s} giây`)
      }
      if (key === ' ' || key === 'k') {
        if (v.paused) { void v.play().catch(() => undefined); showFlash('Phát') } else { v.pause(); showFlash('Tạm dừng') }
      } else if (key === 'arrowleft' || key === 'j') seekBy(-10)
      else if (key === 'arrowright' || key === 'l') seekBy(10)
      else if (e.key === '>' || (e.shiftKey && key === '.')) changeRate(1)
      else if (e.key === '<' || (e.shiftKey && key === ',')) changeRate(-1)
      else if (key === 'm') { v.muted = !v.muted; showFlash(v.muted ? 'Đã tắt tiếng' : 'Đã bật tiếng') }
      else if (key === 'c' && lesson.subtitleUrl) setCaptions((c) => { showFlash(c ? 'Tắt phụ đề' : 'Bật phụ đề'); return !c })
      else if (key === 'f') {
        if (document.fullscreenElement) void document.exitFullscreen().catch(() => undefined)
        else void wrapRef.current?.requestFullscreen?.().catch(() => undefined)
      } else return
      e.preventDefault()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [changeRate, showFlash, lesson.subtitleUrl])

  const ended = () => {
    onEnded()
    if (autoNext && lesson.nextLessonId) setCountdown(COUNTDOWN)
  }

  return (
    <div className="bg-black">
      <div ref={wrapRef} className="relative bg-black">
        {videoError && (
          <div className="absolute inset-0 z-[2] flex flex-col items-center justify-center gap-3 bg-ink/90 p-6 text-center text-white" role="alert">
            <AlertTriangle className="size-10 text-sun-300" aria-hidden="true" />
            <p className="max-w-md font-semibold">Không phát được video. Có thể kết nối mạng chập chờn hoặc liên kết video đã hết hạn.</p>
            <button type="button" className="btn-primary" onClick={() => { setVideoError(false); onReload() }}>Tải lại video</button>
            <p className="text-sm text-white/70">Nếu vẫn lỗi, hãy đặt câu hỏi ở mục "Hỏi đáp" để giảng viên kiểm tra.</p>
          </div>
        )}
        <video
          onError={() => setVideoError(true)}
          key={lesson.id}
          ref={videoRef}
          src={lesson.videoUrl ?? undefined}
          controls
          playsInline
          className="mx-auto aspect-video max-h-[70svh] w-full"
          onLoadedMetadata={(e) => {
            const v = e.currentTarget
            v.playbackRate = rate
            applyCaptions(captions)
            if (lesson.lastPositionSeconds > 5 && lesson.lastPositionSeconds < lesson.durationSeconds - 5) v.currentTime = lesson.lastPositionSeconds
          }}
          onPlay={() => setCountdown(null)}
          onEnded={ended}
        >
          {lesson.subtitleUrl && <track kind="subtitles" srcLang="vi" label="Tiếng Việt" src={lesson.subtitleUrl} default={captions} />}
        </video>

        {flash && (
          <div className="pointer-events-none absolute top-6 left-1/2 z-[1] -translate-x-1/2 rounded-full bg-ink/80 px-4 py-2 text-sm font-semibold text-white tabular-nums" aria-hidden="true">
            {flash}
          </div>
        )}

        {countdown !== null && lesson.nextLessonId && (
          <div className="absolute inset-0 z-[2] flex items-center justify-center bg-ink/85 p-6 text-white" role="status" aria-live="polite">
            <div className="flex max-w-md flex-col items-center gap-4 text-center">
              <p className="text-sm text-white/70">Bài tiếp theo sau <span className="font-bold text-white tabular-nums">{countdown}</span> giây</p>
              <p className="text-xl font-semibold">{lesson.nextLessonTitle ?? 'Bài học tiếp theo'}</p>
              <div className="flex gap-3">
                <button type="button" className="btn-primary" onClick={() => { setCountdown(null); onNext() }}>
                  <SkipForward className="size-4" aria-hidden="true" />Phát ngay
                </button>
                <button type="button" className="btn bg-white/10 text-white hover:bg-white/20" onClick={() => setCountdown(null)}>
                  <X className="size-4" aria-hidden="true" />Ở lại bài này
                </button>
              </div>
            </div>
          </div>
        )}
      </div>

      <div className="relative mx-auto flex max-w-5xl flex-wrap items-center gap-x-4 gap-y-2 px-4 py-2 text-sm text-white/85">
        <label className="flex items-center gap-2">
          <span>Tốc độ</span>
          <select value={rate} onChange={(e) => setRate(Number(e.target.value))} aria-label="Tốc độ phát"
            className="rounded-(--radius-control) bg-white/10 px-2 py-1 text-white tabular-nums focus-visible:outline-2 focus-visible:outline-sun-300 [&>option]:text-ink">
            {RATES.map((r) => <option key={r} value={r}>{String(r).replace('.', ',')}x</option>)}
          </select>
        </label>
        {lesson.subtitleUrl && (
          <button type="button" aria-pressed={captions} onClick={() => setCaptions((c) => !c)}
            className={`flex items-center gap-1.5 rounded-(--radius-control) px-2 py-1 ${captions ? 'bg-white/20 text-white' : 'hover:bg-white/10'}`}>
            <Captions className="size-4" aria-hidden="true" />Phụ đề
          </button>
        )}
        <label className="flex cursor-pointer items-center gap-2">
          <input type="checkbox" checked={autoNext} onChange={(e) => setAutoNext(e.target.checked)} className="size-4 accent-sun-300" />
          Tự chuyển bài
        </label>
        <button type="button" className="ml-auto flex items-center gap-1.5 rounded-(--radius-control) px-2 py-1 hover:bg-white/10"
          aria-expanded={helpOpen} aria-controls="shortcut-help" onClick={() => setHelpOpen((o) => !o)}>
          <Keyboard className="size-4" aria-hidden="true" />Phím tắt
        </button>
        {helpOpen && (
          <div id="shortcut-help" className="absolute right-4 bottom-full z-[3] mb-2 w-72 rounded-(--radius-card) bg-white p-4 text-ink shadow-lg">
            <p className="mb-2 font-semibold">Phím tắt trình phát</p>
            <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1.5 text-sm">
              {shortcuts.map(([k, d]) => (
                <div key={k} className="contents">
                  <dt><kbd className="rounded bg-surface-2 px-1.5 py-0.5 font-mono text-xs">{k}</kbd></dt>
                  <dd className="text-ink-soft">{d}</dd>
                </div>
              ))}
            </dl>
          </div>
        )}
      </div>
    </div>
  )
})
