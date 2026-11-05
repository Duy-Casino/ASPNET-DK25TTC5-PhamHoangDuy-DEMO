import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { CheckCircle2, Clock, XCircle } from 'lucide-react'
import { toast } from 'sonner'
import { ErrorState, Skeleton, Spinner } from '../../components/ui'
import { useAsync } from '../../hooks/useAsync'
import { learningService } from '../../services/dashboardApi'
import { getErrorMessage } from '../../services/apiClient'
import type { AttemptResult, AttemptTake } from '../../types/dashboard'
import { formatDateTime } from '../../utils/format'
import { useLearn } from './LearnPage'

type Answers = Record<string, string[]>

const storageKey = (attemptId: string) => `eduviet-quiz-${attemptId}`
function loadAnswers(attemptId: string): Answers {
  try {
    return JSON.parse(sessionStorage.getItem(storageKey(attemptId)) ?? '{}') as Answers
  } catch {
    return {}
  }
}
function saveAnswers(attemptId: string, answers: Answers) {
  try {
    sessionStorage.setItem(storageKey(attemptId), JSON.stringify(answers))
  } catch {
    // Trình duyệt chặn storage: bỏ qua, bài vẫn làm được
  }
}

export default function QuizView() {
  const { quizId = '' } = useParams()
  const { course, refresh } = useLearn()
  const intro = useAsync(() => learningService.quizIntro(quizId), [quizId])
  const [take, setTake] = useState<AttemptTake | null>(null)
  const [result, setResult] = useState<AttemptResult | null>(null)
  const [starting, setStarting] = useState(false)

  useEffect(() => {
    setTake(null)
    setResult(null)
  }, [quizId])

  const start = async () => {
    setStarting(true)
    try {
      setTake(await learningService.startAttempt(quizId))
      setResult(null)
    } catch (e) {
      toast.error(getErrorMessage(e))
    } finally {
      setStarting(false)
    }
  }

  const showResult = async (attemptId: string) => {
    try {
      setResult(await learningService.attemptResult(attemptId))
    } catch (e) {
      toast.error(getErrorMessage(e))
    }
  }

  const onSubmitted = (r: AttemptResult) => {
    setTake(null)
    setResult(r)
    intro.reload()
    refresh()
    if (r.courseCompleted && r.isPassed) toast.success('Chúc mừng! Bạn đã hoàn thành khóa học.')
  }

  if (course.isPreviewMode)
    return <div className="mx-auto max-w-3xl p-8"><p className="bg-sun-100 p-6">Chế độ xem trước không làm bài kiểm tra. Hãy soạn đề trong trang quản lý khóa học.</p></div>
  if (intro.error) return <div className="p-6 sm:p-10"><ErrorState message={intro.error} onRetry={intro.reload} /></div>
  if (intro.loading && !intro.data) return <div className="p-10"><Skeleton className="h-64 max-w-3xl" /></div>
  const q = intro.data!

  if (take) return <TakeQuiz take={take} onSubmitted={onSubmitted} />
  if (result) return <ResultView result={result} canRetry={q.canAttempt} onRetry={start} retrying={starting} />

  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-8">
      <h1 className="text-title font-extrabold">{q.title}</h1>
      {q.description && <p className="mt-4 text-lg text-ink-soft">{q.description}</p>}
      <dl className="mt-8 grid grid-cols-2 gap-4 sm:grid-cols-4">
        <Fact label="Số câu hỏi" value={String(q.questionCount)} />
        <Fact label="Điểm đạt" value={`${q.passingScore}%`} />
        <Fact label="Thời gian" value={q.timeLimitMinutes ? `${q.timeLimitMinutes} phút` : 'Không giới hạn'} />
        <Fact label="Lượt làm" value={q.maxAttempts ? `${q.attemptsUsed}/${q.maxAttempts}` : `${q.attemptsUsed} lượt`} />
      </dl>

      {q.isPassed && <p className="mt-8 bg-success-50 p-4 font-semibold text-success">Bạn đã đạt bài kiểm tra này với điểm cao nhất {Math.round(q.bestScore ?? 0)}%.</p>}

      <div className="mt-8 flex flex-wrap gap-3">
        {q.canAttempt ? (
          <button type="button" className="btn-primary min-h-12 px-8 text-base" onClick={start} disabled={starting}>
            {starting && <Spinner className="size-4" />}
            {q.inProgressAttemptId ? 'Tiếp tục làm bài' : q.attemptsUsed > 0 ? 'Làm lại' : 'Bắt đầu làm bài'}
          </button>
        ) : (
          <p className="bg-surface-2 p-4 text-ink-soft">Bạn đã dùng hết số lượt làm bài.</p>
        )}
      </div>

      {q.attempts.length > 0 && (
        <section className="mt-12" aria-labelledby="hist-h">
          <h2 id="hist-h" className="text-xl font-bold">Lịch sử làm bài</h2>
          <ul className="mt-4 flex flex-col gap-2">
            {q.attempts.map((a) => (
              <li key={a.id}>
                <button type="button" onClick={() => showResult(a.id)} className="flex w-full items-center gap-4 bg-surface-2 px-4 py-3 text-left hover:bg-brand-50">
                  <span className="font-semibold">Lượt {a.attemptNumber}</span>
                  <span className="text-sm text-ink-soft">{a.submittedAt && formatDateTime(a.submittedAt)}</span>
                  <span className={`ml-auto font-bold tabular-nums ${a.isPassed ? 'text-success' : 'text-danger'}`}>
                    {Math.round(a.scorePercent)}% · {a.isPassed ? 'Đạt' : 'Chưa đạt'}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-surface-2 p-4">
      <dt className="text-sm text-ink-soft">{label}</dt>
      <dd className="mt-1 text-xl font-extrabold">{value}</dd>
    </div>
  )
}

function TakeQuiz({ take, onSubmitted }: { take: AttemptTake; onSubmitted: (r: AttemptResult) => void }) {
  const [answers, setAnswers] = useState<Answers>(() => loadAnswers(take.attemptId))
  const [submitting, setSubmitting] = useState(false)
  const [remaining, setRemaining] = useState<number | null>(null)
  const submittedRef = useRef(false)

  const submit = useCallback(async (auto = false) => {
    if (submittedRef.current) return
    submittedRef.current = true
    setSubmitting(true)
    try {
      const payload = Object.entries(answers).map(([questionId, optionIds]) => ({ questionId, optionIds }))
      const r = await learningService.submitAttempt(take.attemptId, payload)
      try { sessionStorage.removeItem(storageKey(take.attemptId)) } catch { /* ignore */ }
      if (auto) toast.info('Hết giờ — bài đã được tự động nộp.')
      onSubmitted(r)
    } catch (e) {
      submittedRef.current = false
      toast.error(getErrorMessage(e))
    } finally {
      setSubmitting(false)
    }
  }, [answers, take.attemptId, onSubmitted])

  // Đồng hồ đếm ngược; hết giờ tự nộp
  useEffect(() => {
    if (!take.deadline) return
    const end = new Date(take.deadline).getTime()
    const tick = () => {
      const left = Math.max(0, Math.floor((end - Date.now()) / 1000))
      setRemaining(left)
      if (left === 0) void submit(true)
    }
    tick()
    const id = window.setInterval(tick, 1000)
    return () => window.clearInterval(id)
  }, [take.deadline, submit])

  const choose = (questionId: string, optionId: string, multiple: boolean) => {
    setAnswers((prev) => {
      const current = prev[questionId] ?? []
      const next = multiple
        ? current.includes(optionId) ? current.filter((x) => x !== optionId) : [...current, optionId]
        : [optionId]
      const updated = { ...prev, [questionId]: next }
      saveAnswers(take.attemptId, updated)
      return updated
    })
  }

  const answered = take.questions.filter((q) => (answers[q.id]?.length ?? 0) > 0).length

  const confirmSubmit = () => {
    const missing = take.questions.length - answered
    if (missing > 0 && !window.confirm(`Bạn còn ${missing} câu chưa trả lời. Vẫn nộp bài?`)) return
    void submit()
  }

  return (
    <div className="mx-auto max-w-3xl px-4 py-8 sm:px-8">
      <div className="sticky top-16 z-10 -mx-4 flex items-center gap-4 bg-white/95 px-4 py-3 sm:-mx-8 sm:px-8">
        <p className="font-bold">{take.quizTitle} · Lượt {take.attemptNumber}</p>
        <span className="ml-auto text-sm text-ink-soft tabular-nums">{answered}/{take.questions.length} câu</span>
        {remaining !== null && (
          <span className={`flex items-center gap-1.5 rounded-(--radius-control) px-3 py-1 font-bold tabular-nums ${remaining < 60 ? 'bg-danger-50 text-danger' : 'bg-brand-50 text-brand-800'}`} role="timer" aria-live="off">
            <Clock className="size-4" aria-hidden="true" />
            {Math.floor(remaining / 60)}:{String(remaining % 60).padStart(2, '0')}
          </span>
        )}
      </div>

      <ol className="mt-6 flex flex-col gap-8">
        {take.questions.map((q, i) => {
          const multiple = q.type === 'MultipleChoice'
          return (
            <li key={q.id}>
              <fieldset>
                <legend className="text-lg font-bold">
                  <span className="text-ink-muted">Câu {i + 1}.</span> {q.content}
                  <span className="ml-2 text-sm font-normal text-ink-muted">({q.points} điểm{multiple ? ', chọn nhiều đáp án' : ''})</span>
                </legend>
                <div className="mt-4 flex flex-col gap-2">
                  {q.options.map((o) => {
                    const checked = answers[q.id]?.includes(o.id) ?? false
                    return (
                      <label key={o.id} className={`flex min-h-12 cursor-pointer items-center gap-3 rounded-xl px-4 py-3 transition-colors ${checked ? 'bg-brand-100 font-semibold' : 'bg-surface-2 hover:bg-surface-3'}`}>
                        <input type={multiple ? 'checkbox' : 'radio'} name={q.id} checked={checked} onChange={() => choose(q.id, o.id, multiple)} className="size-5 accent-brand-600" />
                        {o.content}
                      </label>
                    )
                  })}
                </div>
              </fieldset>
            </li>
          )
        })}
      </ol>

      <div className="mt-10 flex justify-end">
        <button type="button" className="btn-primary min-h-12 px-8 text-base" onClick={confirmSubmit} disabled={submitting}>
          {submitting && <Spinner className="size-4" />} Nộp bài
        </button>
      </div>
    </div>
  )
}

function ResultView({ result, canRetry, onRetry, retrying }: { result: AttemptResult; canRetry: boolean; onRetry: () => void; retrying: boolean }) {
  return (
    <div className="mx-auto max-w-3xl px-4 py-10 sm:px-8">
      <div className={`p-6 sm:p-8 ${result.isPassed ? 'bg-success-50' : 'bg-danger-50'}`}>
        <p className={`flex items-center gap-2 font-bold ${result.isPassed ? 'text-success' : 'text-danger'}`}>
          {result.isPassed ? <CheckCircle2 className="size-5" aria-hidden="true" /> : <XCircle className="size-5" aria-hidden="true" />}
          {result.isPassed ? 'Đạt' : 'Chưa đạt'} — lượt {result.attemptNumber}
        </p>
        <p className="mt-2 text-6xl font-extrabold tabular-nums">{Math.round(result.scorePercent)}%</p>
        <p className="mt-2 text-ink-soft">{result.score}/{result.maxScore} điểm · cần {result.passingScore}% để đạt</p>
        {result.courseCompleted && result.isPassed && (
          <p className="mt-4 font-semibold">
            Bạn đã hoàn thành toàn bộ khóa học.{' '}
            {result.certificateCode
              ? <>Chứng chỉ <Link to={`/xac-minh/${result.certificateCode}`} className="text-brand-700 underline">{result.certificateCode}</Link> đã được cấp — <Link to="/hoc-vien/chung-chi" className="text-brand-700 underline">tải PDF</Link>.</>
              : 'Chứng chỉ sẽ được cấp ngay khi hệ thống ghi nhận đủ điều kiện.'}
          </p>
        )}
        <div className="mt-6 flex flex-wrap gap-3">
          {canRetry && !result.isPassed && (
            <button type="button" className="btn-primary" onClick={onRetry} disabled={retrying}>{retrying && <Spinner className="size-4" />}Làm lại</button>
          )}
          <Link to={`/hoc/${result.courseSlug}`} className="btn-soft">Quay lại bài học</Link>
        </div>
      </div>

      <h2 className="mt-10 text-xl font-bold">Đáp án chi tiết</h2>
      <ol className="mt-6 flex flex-col gap-8">
        {result.questions.map((q, i) => (
          <li key={q.questionId}>
            <p className="font-bold">
              {q.isCorrect
                ? <CheckCircle2 className="mr-1 inline size-5 text-success" aria-label="Đúng" />
                : <XCircle className="mr-1 inline size-5 text-danger" aria-label="Sai" />}
              Câu {i + 1}. {q.content}
              <span className="ml-2 text-sm font-normal text-ink-muted">{q.pointsAwarded}/{q.points} điểm</span>
            </p>
            <ul className="mt-3 flex flex-col gap-2">
              {q.options.map((o) => (
                <li key={o.id} className={`flex items-center gap-3 rounded-xl px-4 py-3 ${o.isCorrect ? 'bg-success-50 font-semibold' : o.selected ? 'bg-danger-50' : 'bg-surface-2'}`}>
                  <span className="w-28 shrink-0 text-sm">{o.isCorrect ? 'Đáp án đúng' : o.selected ? 'Bạn chọn' : ''}</span>
                  {o.content}
                </li>
              ))}
            </ul>
            {q.explanation && <p className="mt-3 text-ink-soft">Giải thích: {q.explanation}</p>}
          </li>
        ))}
      </ol>
    </div>
  )
}
