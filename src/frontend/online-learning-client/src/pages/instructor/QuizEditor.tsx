import { useState, type FormEvent } from 'react'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { Modal, Panel, SelectField, Toggle } from '../../components/dashboard'
import { EmptyState, ErrorState, Field, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync } from '../../hooks/useAsync'
import { getErrorMessage } from '../../services/apiClient'
import { instructorService } from '../../services/dashboardApi'
import type { CourseEdit, QuestionType, SaveQuizInput } from '../../types/dashboard'

const typeLabel: Record<QuestionType, string> = { SingleChoice: 'Một đáp án', MultipleChoice: 'Nhiều đáp án', TrueFalse: 'Đúng / Sai' }

const emptyQuestion = (): SaveQuizInput['questions'][number] => ({
  content: '', type: 'SingleChoice', points: 1, explanation: null,
  options: [{ content: '', isCorrect: true }, { content: '', isCorrect: false }],
})

/** Tab bài kiểm tra trong trang soạn khóa học. */
export default function QuizzesTab({ course }: { course: CourseEdit }) {
  const { data, loading, error, reload } = useAsync(() => instructorService.quizzes(course.id), [course.id])
  const [editing, setEditing] = useState<string | 'new' | null>(null)

  const remove = async (id: string, title: string) => {
    if (!window.confirm(`Xóa bài kiểm tra "${title}"?`)) return
    try {
      await instructorService.deleteQuiz(id)
      toast.success('Đã xóa bài kiểm tra')
      reload()
    } catch (e) {
      toast.error(getErrorMessage(e))
    }
  }

  return (
    <div className="flex flex-col gap-4">
      {error ? <ErrorState message={error} onRetry={reload} />
        : loading ? <Skeleton className="h-40" />
        : data?.length === 0 ? (
          <EmptyState title="Chưa có bài kiểm tra"
            action={course.isEditable && <button type="button" className="btn-primary" onClick={() => setEditing('new')}>Tạo bài kiểm tra</button>}>
            Bài kiểm tra bắt buộc phải đạt thì học viên mới hoàn thành khóa học và nhận chứng chỉ.
          </EmptyState>
        ) : (
          <>
            {data?.map((q) => (
              <Panel key={q.id}>
                <div className="flex flex-wrap items-center gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="text-lg font-bold">{q.title}</p>
                    <p className="text-sm text-ink-soft">
                      {q.questionCount} câu · đạt {q.passingScore}% · {q.maxAttempts ? `${q.maxAttempts} lượt` : 'không giới hạn lượt'}
                      {q.isRequired && ' · bắt buộc'}{q.isFinalExam && ' · cuối khóa'}{q.sectionTitle && ` · ${q.sectionTitle}`} · {q.attemptCount} lượt làm
                    </p>
                  </div>
                  <button type="button" className="btn-soft" onClick={() => setEditing(q.id)}><Pencil className="size-4" aria-hidden="true" />{course.isEditable ? 'Sửa' : 'Xem'}</button>
                  {course.isEditable && q.attemptCount === 0 && (
                    <button type="button" className="btn-ghost text-danger" onClick={() => remove(q.id, q.title)}><Trash2 className="size-4" aria-hidden="true" />Xóa</button>
                  )}
                </div>
              </Panel>
            ))}
            {course.isEditable && <button type="button" className="btn-primary w-fit" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" />Thêm bài kiểm tra</button>}
          </>
        )}
      {editing && <QuizModal course={course} quizId={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </div>
  )
}

function QuizModal({ course, quizId, onClose, onSaved }: { course: CourseEdit; quizId: string | null; onClose: () => void; onSaved: () => void }) {
  const existing = useAsync(() => (quizId ? instructorService.quiz(quizId) : Promise.resolve(null)), [quizId])
  if (quizId && (existing.loading || !existing.data)) {
    return <Modal title="Bài kiểm tra" onClose={onClose} wide>{existing.error ? <ErrorState message={existing.error} /> : <Skeleton className="h-64" />}</Modal>
  }
  const q = existing.data
  const initial: SaveQuizInput = q
    ? {
        title: q.title, description: q.description, sectionId: q.sectionId, passingScore: q.passingScore, maxAttempts: q.maxAttempts,
        timeLimitMinutes: q.timeLimitMinutes, isRequired: q.isRequired, isFinalExam: q.isFinalExam, shuffleQuestions: q.shuffleQuestions,
        questions: q.questions.map((x) => ({ content: x.content, type: x.type, points: x.points, explanation: x.explanation, options: x.options.map((o) => ({ content: o.content, isCorrect: o.isCorrect })) })),
      }
    : { title: '', description: null, sectionId: null, passingScore: 70, maxAttempts: 3, timeLimitMinutes: 15, isRequired: true, isFinalExam: true, shuffleQuestions: false, questions: [emptyQuestion()] }
  return <QuizForm course={course} quizId={quizId} initial={initial} attemptCount={q?.attemptCount ?? 0} onClose={onClose} onSaved={onSaved} />
}

function QuizForm({ course, quizId, initial, attemptCount, onClose, onSaved }: {
  course: CourseEdit; quizId: string | null; initial: SaveQuizInput; attemptCount: number; onClose: () => void; onSaved: () => void
}) {
  const [f, setF] = useState<SaveQuizInput>(initial)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const readOnly = !course.isEditable
  const questionsLocked = attemptCount > 0

  const updateQ = (i: number, patch: Partial<SaveQuizInput['questions'][number]>) =>
    setF((x) => ({ ...x, questions: x.questions.map((q, qi) => (qi === i ? { ...q, ...patch } : q)) }))

  const setType = (i: number, type: QuestionType) => {
    const q = f.questions[i]
    let options = q.options
    if (type === 'TrueFalse') options = [{ content: 'Đúng', isCorrect: true }, { content: 'Sai', isCorrect: false }]
    else if (type === 'SingleChoice') {
      const first = Math.max(0, options.findIndex((o) => o.isCorrect))
      options = options.map((o, oi) => ({ ...o, isCorrect: oi === first }))
    }
    updateQ(i, { type, options })
  }

  const toggleCorrect = (qi: number, oi: number) => {
    const q = f.questions[qi]
    const options = q.type === 'MultipleChoice'
      ? q.options.map((o, i) => (i === oi ? { ...o, isCorrect: !o.isCorrect } : o))
      : q.options.map((o, i) => ({ ...o, isCorrect: i === oi }))
    updateQ(qi, { options })
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setFormError(null)
    try {
      if (quizId) await instructorService.updateQuiz(quizId, f)
      else await instructorService.createQuiz(course.id, f)
      toast.success('Đã lưu bài kiểm tra')
      onSaved()
    } catch (err) {
      setFormError(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={quizId ? 'Sửa bài kiểm tra' : 'Tạo bài kiểm tra'} onClose={onClose} wide>
      <form onSubmit={submit}>
        <fieldset disabled={readOnly} className="flex flex-col gap-5">
          {formError && <p role="alert" className="bg-danger-50 p-4 font-medium text-danger">{formError}</p>}
          <Field label="Tên bài kiểm tra" value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} maxLength={200} autoFocus />
          <TextArea label="Mô tả" value={f.description ?? ''} onChange={(e) => setF({ ...f, description: e.target.value || null })} maxLength={1000} rows={2} />
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
            <SelectField label="Thuộc chương" value={f.sectionId ?? ''} onChange={(v) => setF({ ...f, sectionId: v || null })}>
              <option value="">Cuối khóa (không theo chương)</option>
              {course.sections.map((s) => <option key={s.id} value={s.id}>{s.title}</option>)}
            </SelectField>
            <Field label="Điểm đạt (%)" type="number" min={0} max={100} value={f.passingScore} onChange={(e) => setF({ ...f, passingScore: Number(e.target.value) })} />
            <Field label="Số lượt tối đa" type="number" min={1} value={f.maxAttempts ?? ''} onChange={(e) => setF({ ...f, maxAttempts: e.target.value ? Number(e.target.value) : null })} hint="Trống = không giới hạn" />
            <Field label="Thời gian (phút)" type="number" min={1} value={f.timeLimitMinutes ?? ''} onChange={(e) => setF({ ...f, timeLimitMinutes: e.target.value ? Number(e.target.value) : null })} hint="Trống = không giới hạn" />
          </div>
          <div className="flex flex-wrap gap-6">
            <Toggle checked={f.isRequired} onChange={(v) => setF({ ...f, isRequired: v })} label="Bắt buộc để hoàn thành" />
            <Toggle checked={f.isFinalExam} onChange={(v) => setF({ ...f, isFinalExam: v })} label="Bài thi cuối khóa" />
            <Toggle checked={f.shuffleQuestions} onChange={(v) => setF({ ...f, shuffleQuestions: v })} label="Trộn thứ tự câu hỏi" />
          </div>
        </fieldset>

        {questionsLocked && <p className="mt-6 bg-warning-50 p-4 text-sm">Đã có {attemptCount} lượt làm bài nên câu hỏi và đáp án bị khóa; bạn vẫn sửa được thông tin chung và lời giải thích.</p>}

        <fieldset disabled={readOnly} className="mt-8 flex flex-col gap-6">
          <legend className="mb-4 text-lg font-bold">Câu hỏi ({f.questions.length})</legend>
          {f.questions.map((q, qi) => (
            <div key={qi} className="rounded-(--radius-card) bg-surface-2 p-5">
              <div className="flex flex-wrap items-center gap-3">
                <p className="font-bold">Câu {qi + 1}</p>
                <div className="ml-auto flex flex-wrap items-center gap-3">
                  <select aria-label="Loại câu hỏi" value={q.type} disabled={questionsLocked} onChange={(e) => setType(qi, e.target.value as QuestionType)} className="input min-h-10 w-auto">
                    {(Object.keys(typeLabel) as QuestionType[]).map((t) => <option key={t} value={t}>{typeLabel[t]}</option>)}
                  </select>
                  <label className="flex items-center gap-2 text-sm">Điểm
                    <input type="number" min={0.5} step={0.5} value={q.points} disabled={questionsLocked} onChange={(e) => updateQ(qi, { points: Number(e.target.value) })} className="input min-h-10 w-20" />
                  </label>
                  {!questionsLocked && f.questions.length > 1 && (
                    <button type="button" className="rounded-lg p-2 text-danger hover:bg-danger-50" aria-label={`Xóa câu ${qi + 1}`}
                      onClick={() => setF({ ...f, questions: f.questions.filter((_, i) => i !== qi) })}><Trash2 className="size-4" /></button>
                  )}
                </div>
              </div>
              <div className="mt-4 flex flex-col gap-3">
                <TextArea label="Nội dung câu hỏi" value={q.content} disabled={questionsLocked} onChange={(e) => updateQ(qi, { content: e.target.value })} rows={2} maxLength={2000} />
                <p className="text-sm font-semibold">Đáp án — chọn {q.type === 'MultipleChoice' ? 'các' : 'một'} đáp án đúng</p>
                {q.options.map((o, oi) => (
                  <div key={oi} className="flex items-center gap-3">
                    <input type={q.type === 'MultipleChoice' ? 'checkbox' : 'radio'} name={`correct-${qi}`} checked={o.isCorrect} disabled={questionsLocked}
                      onChange={() => toggleCorrect(qi, oi)} className="size-5 accent-success" aria-label={`Đáp án ${oi + 1} đúng`} />
                    <input value={o.content} disabled={questionsLocked || q.type === 'TrueFalse'} maxLength={1000} placeholder={`Đáp án ${oi + 1}`} aria-label={`Nội dung đáp án ${oi + 1}`}
                      onChange={(e) => updateQ(qi, { options: q.options.map((x, i) => (i === oi ? { ...x, content: e.target.value } : x)) })}
                      className="input min-h-10 flex-1 bg-white" />
                    {!questionsLocked && q.type !== 'TrueFalse' && q.options.length > 2 && (
                      <button type="button" className="rounded-lg p-2 text-ink-soft hover:bg-surface-3" aria-label={`Xóa đáp án ${oi + 1}`}
                        onClick={() => updateQ(qi, { options: q.options.filter((_, i) => i !== oi) })}><Trash2 className="size-4" /></button>
                    )}
                  </div>
                ))}
                {!questionsLocked && q.type !== 'TrueFalse' && q.options.length < 10 && (
                  <button type="button" className="btn-ghost w-fit px-3 text-brand-700" onClick={() => updateQ(qi, { options: [...q.options, { content: '', isCorrect: false }] })}>
                    <Plus className="size-4" aria-hidden="true" />Thêm đáp án
                  </button>
                )}
                <Field label="Giải thích (hiện sau khi nộp bài)" value={q.explanation ?? ''} onChange={(e) => updateQ(qi, { explanation: e.target.value || null })} maxLength={2000} />
              </div>
            </div>
          ))}
          {!questionsLocked && (
            <button type="button" className="btn-soft w-fit" onClick={() => setF({ ...f, questions: [...f.questions, emptyQuestion()] })}>
              <Plus className="size-4" aria-hidden="true" />Thêm câu hỏi
            </button>
          )}
        </fieldset>

        {!readOnly && (
          <div className="sticky bottom-0 -mx-6 mt-8 bg-white px-6 py-4 sm:-mx-8 sm:px-8">
            <button type="submit" className="btn-primary min-h-12 w-full" disabled={saving}>{saving && <Spinner className="size-4" />}Lưu bài kiểm tra</button>
          </div>
        )}
      </form>
    </Modal>
  )
}
