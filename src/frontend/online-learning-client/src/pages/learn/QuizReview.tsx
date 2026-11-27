import { useState } from 'react'
import { CheckCircle2, RotateCcw, XCircle } from 'lucide-react'
import type { AttemptResult } from '../../types/dashboard'

type Question = AttemptResult['questions'][number]

const shuffle = <T,>(items: T[]) => {
  const a = [...items]
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]]
  }
  return a
}

/**
 * Luyện lại các câu đã trả lời sai: làm từng câu, bấm "Kiểm tra" để xem đúng/sai và giải thích.
 * Chỉ để ôn tập trên trình duyệt, không tính điểm và không tốn lượt làm bài.
 */
export function PracticeWrong({ questions, onClose }: { questions: Question[]; onClose: () => void }) {
  const [order, setOrder] = useState(() => shuffle(questions))
  const [index, setIndex] = useState(0)
  const [picked, setPicked] = useState<string[]>([])
  const [checked, setChecked] = useState(false)
  const [correctCount, setCorrectCount] = useState(0)

  const restart = () => { setOrder(shuffle(questions)); setIndex(0); setPicked([]); setChecked(false); setCorrectCount(0) }

  if (index >= order.length) {
    return (
      <section className="mt-10 rounded-(--radius-card) bg-brand-50 p-6" aria-live="polite">
        <h2 className="text-xl font-bold">Kết quả luyện tập</h2>
        <p className="mt-2 text-lg">Bạn trả lời đúng <b className="tabular-nums">{correctCount}/{order.length}</b> câu đã sai trước đó.</p>
        <p className="mt-1 text-ink-soft">{correctCount === order.length ? 'Rất tốt! Bạn đã sẵn sàng làm lại bài kiểm tra.' : 'Hãy xem lại bài học liên quan rồi luyện thêm một lượt.'}</p>
        <div className="mt-4 flex flex-wrap gap-3">
          <button type="button" className="btn-primary" onClick={restart}><RotateCcw className="size-4" aria-hidden="true" />Luyện lại</button>
          <button type="button" className="btn-soft" onClick={onClose}>Xem đáp án chi tiết</button>
        </div>
      </section>
    )
  }

  const q = order[index]
  const multiple = q.type === 'MultipleChoice'
  const correctIds = q.options.filter((o) => o.isCorrect).map((o) => o.id)
  const isRight = picked.length === correctIds.length && picked.every((id) => correctIds.includes(id))
  const toggle = (id: string) => {
    if (checked) return
    setPicked((p) => multiple ? (p.includes(id) ? p.filter((x) => x !== id) : [...p, id]) : [id])
  }
  const check = () => { setChecked(true); if (isRight) setCorrectCount((c) => c + 1) }
  const next = () => { setIndex((i) => i + 1); setPicked([]); setChecked(false) }

  return (
    <section className="mt-10 rounded-(--radius-card) bg-surface-2 p-6" aria-labelledby="practice-h">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 id="practice-h" className="text-xl font-bold">Luyện lại câu sai</h2>
        <span className="text-sm text-ink-soft tabular-nums">Câu {index + 1}/{order.length} · không tính điểm</span>
      </div>
      <fieldset className="mt-4">
        <legend className="text-lg font-semibold">{q.content}{multiple && <span className="ml-2 text-sm font-normal text-ink-muted">(chọn nhiều đáp án)</span>}</legend>
        <div className="mt-4 flex flex-col gap-2">
          {q.options.map((o) => {
            const on = picked.includes(o.id)
            const tone = !checked ? (on ? 'bg-brand-100 font-semibold' : 'bg-white hover:bg-surface-3')
              : o.isCorrect ? 'bg-success-50 font-semibold' : on ? 'bg-danger-50' : 'bg-white'
            return (
              <label key={o.id} className={`flex min-h-12 cursor-pointer items-center gap-3 rounded-xl px-4 py-3 transition-colors ${tone}`}>
                <input type={multiple ? 'checkbox' : 'radio'} name={`practice-${q.questionId}`} checked={on} disabled={checked}
                  onChange={() => toggle(o.id)} className="size-5 accent-brand-600" />
                <span className="flex-1">{o.content}</span>
                {checked && o.isCorrect && <span className="text-sm text-success">Đáp án đúng</span>}
              </label>
            )
          })}
        </div>
      </fieldset>
      {checked && (
        <div className={`mt-4 rounded-xl p-4 ${isRight ? 'bg-success-50' : 'bg-danger-50'}`} role="status">
          <p className={`flex items-center gap-2 font-semibold ${isRight ? 'text-success' : 'text-danger'}`}>
            {isRight ? <CheckCircle2 className="size-5" aria-hidden="true" /> : <XCircle className="size-5" aria-hidden="true" />}
            {isRight ? 'Chính xác!' : 'Chưa đúng'}
          </p>
          {q.explanation && <p className="mt-1 text-ink-soft">Giải thích: {q.explanation}</p>}
        </div>
      )}
      <div className="mt-6 flex flex-wrap gap-3">
        {!checked
          ? <button type="button" className="btn-primary" disabled={picked.length === 0} onClick={check}>Kiểm tra</button>
          : <button type="button" className="btn-primary" onClick={next}>{index + 1 < order.length ? 'Câu tiếp theo' : 'Xem kết quả'}</button>}
        <button type="button" className="btn-soft" onClick={onClose}>Thoát luyện tập</button>
      </div>
    </section>
  )
}
