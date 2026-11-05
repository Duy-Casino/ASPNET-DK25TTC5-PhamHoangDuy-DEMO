import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { BadgeCheck, Download, ShieldAlert, ShieldCheck } from 'lucide-react'
import { ErrorState, Spinner } from '../components/ui'
import { useDocumentTitle } from '../hooks/useAsync'
import { certificateService, type CertificateVerify } from '../services/certificateApi'
import { getErrorMessage } from '../services/apiClient'
import { formatDate, formatDuration } from '../utils/format'

/**
 * Trang xác minh chứng chỉ công khai (FR-CERT-04). Nhà tuyển dụng nhập mã hoặc quét QR trên PDF.
 * Bố cục là chính tấm chứng chỉ — cùng ngôn ngữ với hero trang chủ.
 */
export default function VerifyCertificatePage() {
  const { code: routeCode } = useParams()
  const navigate = useNavigate()
  const [input, setInput] = useState(routeCode ?? '')
  const [state, setState] = useState<{ loading: boolean; data: CertificateVerify | null; error: string | null; notFound: boolean }>({
    loading: !!routeCode, data: null, error: null, notFound: false,
  })
  useDocumentTitle(state.data ? `Chứng chỉ ${state.data.certificateCode}` : 'Xác minh chứng chỉ')

  useEffect(() => {
    if (!routeCode) {
      setState({ loading: false, data: null, error: null, notFound: false })
      return
    }
    setInput(routeCode)
    let cancelled = false
    setState({ loading: true, data: null, error: null, notFound: false })
    certificateService.verify(routeCode)
      .then((data) => !cancelled && setState({ loading: false, data, error: null, notFound: false }))
      .catch((e) => {
        if (cancelled) return
        const status = (e as { response?: { status?: number } }).response?.status
        setState({ loading: false, data: null, error: status === 404 || status === 400 ? null : getErrorMessage(e), notFound: status === 404 || status === 400 })
      })
    return () => {
      cancelled = true
    }
  }, [routeCode])

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const code = input.trim().toUpperCase()
    if (code) navigate(`/xac-minh/${code}`)
  }

  const c = state.data

  return (
    <div className="bg-brand-700">
      <div className="container-page grid gap-10 py-14 md:py-20 lg:grid-cols-[1fr_1.4fr] lg:items-start">
        <div className="text-white">
          <h1 className="text-title font-extrabold">Xác minh chứng chỉ</h1>
          <p className="mt-4 max-w-md text-lg text-brand-50">
            Mỗi chứng chỉ EduViet có một mã riêng. Nhập mã in trên chứng chỉ hoặc quét mã QR để kiểm tra chứng chỉ là thật và còn hiệu lực.
          </p>
          <form onSubmit={submit} className="mt-8 flex max-w-md flex-col gap-3 sm:flex-row">
            <label htmlFor="cert-code" className="sr-only">Mã chứng chỉ</label>
            <input id="cert-code" value={input} onChange={(e) => setInput(e.target.value)} placeholder="EDV-2026-XXXXXXXX"
              className="input uppercase tabular-nums" autoComplete="off" spellCheck={false} maxLength={30} />
            <button type="submit" className="btn-accent min-h-12 shrink-0" disabled={!input.trim()}>Kiểm tra</button>
          </form>
          <ul className="mt-10 flex max-w-md flex-col gap-4 text-brand-50">
            <li><b className="text-white">Đúng người, đúng khóa:</b> tên học viên và khóa học được lưu cố định tại thời điểm cấp.</li>
            <li><b className="text-white">Còn hiệu lực:</b> chứng chỉ bị thu hồi sẽ hiện rõ ngày và lý do.</li>
            <li><b className="text-white">Bản gốc:</b> tải PDF trực tiếp từ EduViet để đối chiếu với bản ứng viên gửi.</li>
          </ul>
        </div>

        <div aria-live="polite">
          {state.loading && (
            <div className="flex min-h-80 items-center justify-center gap-3 bg-white p-10 text-lg" role="status">
              <Spinner className="size-6 text-brand-600" /> Đang kiểm tra…
            </div>
          )}
          {state.error && <ErrorState message={state.error} />}
          {state.notFound && (
            <div className="flex flex-col gap-4 bg-white p-8 sm:p-10">
              <ShieldAlert className="size-12 text-danger" aria-hidden="true" />
              <h2 className="text-2xl font-extrabold">Không tìm thấy chứng chỉ</h2>
              <p className="text-ink-soft">
                Không có chứng chỉ nào mang mã <b className="text-ink tabular-nums">{routeCode?.toUpperCase()}</b>. Kiểm tra lại các ký tự
                (mã không dùng chữ O, I, L và số 0, 1).
              </p>
            </div>
          )}
          {!state.loading && !c && !state.notFound && !state.error && (
            <div className="flex min-h-80 flex-col justify-center gap-3 bg-white/10 p-10 text-white">
              <BadgeCheck className="size-12 text-sun-300" aria-hidden="true" />
              <p className="text-xl font-bold">Kết quả xác minh sẽ hiện ở đây.</p>
              <p className="text-brand-100">Thông tin hiển thị: tên học viên, khóa học, giảng viên, ngày cấp và tình trạng hiệu lực.</p>
            </div>
          )}

          {c && (
            <article className="rise-in bg-white text-ink">
              <div className={`flex items-center gap-3 px-6 py-4 font-bold sm:px-10 ${c.isRevoked ? 'bg-[#b42318] text-white' : 'bg-[#0b6b39] text-white'}`}>
                {c.isRevoked ? <ShieldAlert className="size-6" aria-hidden="true" /> : <ShieldCheck className="size-6" aria-hidden="true" />}
                {c.isRevoked ? 'Chứng chỉ đã bị thu hồi' : 'Chứng chỉ hợp lệ, do EduViet cấp'}
              </div>
              <div className="px-6 py-8 sm:px-10 sm:py-10">
                <div className="flex items-center justify-between gap-4 text-sm">
                  <span className="text-lg font-extrabold text-brand-600">EduViet</span>
                  <span className="font-semibold text-ink-soft">Chứng chỉ hoàn thành khóa học</span>
                </div>
                <p className="mt-10 text-ink-soft">Chứng nhận</p>
                <p className={`mt-1 text-[clamp(1.8rem,1.2rem+2.4vw,2.8rem)] leading-tight font-extrabold tracking-tight ${c.isRevoked ? 'text-ink-muted line-through' : 'text-brand-700'}`}>
                  {c.studentName}
                </p>
                <p className="mt-6 text-ink-soft">đã hoàn thành khóa học</p>
                <Link to={`/khoa-hoc/${c.courseSlug}`} className="mt-1 block text-2xl font-bold hover:text-brand-700">{c.courseTitle}</Link>

                <dl className="mt-8 grid grid-cols-2 gap-4 bg-brand-50 p-5 sm:grid-cols-4">
                  <div><dt className="text-sm text-ink-soft">Giảng viên</dt><dd className="font-semibold">{c.instructorName}</dd></div>
                  <div><dt className="text-sm text-ink-soft">Ngày cấp</dt><dd className="font-semibold">{formatDate(c.issuedAt)}</dd></div>
                  <div><dt className="text-sm text-ink-soft">Điểm kiểm tra</dt><dd className="font-semibold">{c.finalScore !== null ? `${Math.round(c.finalScore)}%` : '—'}</dd></div>
                  <div><dt className="text-sm text-ink-soft">Thời lượng</dt><dd className="font-semibold">{c.totalLessons} bài · {formatDuration(c.totalDurationSeconds)}</dd></div>
                </dl>

                {c.isRevoked && (
                  <p className="mt-6 bg-danger-50 p-4 text-danger">
                    Thu hồi ngày {c.revokedAt && formatDate(c.revokedAt)}{c.revokedReason && `: ${c.revokedReason}`}
                  </p>
                )}

                <div className="mt-8 flex flex-wrap items-end justify-between gap-4">
                  <div>
                    <p className="text-sm text-ink-soft">Mã chứng chỉ</p>
                    <p className="text-xl font-extrabold tracking-wide tabular-nums">{c.certificateCode}</p>
                  </div>
                  {!c.isRevoked && (
                    <a href={certificateService.publicPdfUrl(c.certificateCode)} className="btn-primary" download>
                      <Download className="size-4" aria-hidden="true" /> Tải bản PDF
                    </a>
                  )}
                </div>
              </div>
            </article>
          )}
        </div>
      </div>
    </div>
  )
}
