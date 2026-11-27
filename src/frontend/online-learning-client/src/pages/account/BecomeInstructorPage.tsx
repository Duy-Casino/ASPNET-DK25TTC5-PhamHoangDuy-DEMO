import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { BadgeCheck, Clock, Megaphone, Wallet } from 'lucide-react'
import { toast } from 'sonner'
import { ErrorState, Field, PageHeader, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { useAuth } from '../../hooks/useAuth'
import { applicationStatusLabel, applicationStatusStyle, instructorApplicationService, publicSettingsService } from '../../services/adminToolsApi'
import { getErrorMessage, refreshSession } from '../../services/apiClient'
import { formatDateTime } from '../../utils/format'

const empty = { headline: '', bio: '', expertise: '', proposedTopic: '', portfolioUrl: '', yearsOfExperience: '' }

/** Người dùng gửi hồ sơ đăng ký giảng dạy; Admin duyệt tại /quan-tri/dang-ky-giang-vien. */
export function BecomeInstructorPage() {
  useDocumentTitle('Trở thành giảng viên')
  const { hasRole } = useAuth()
  const navigate = useNavigate()
  const status = useAsync(() => instructorApplicationService.mine(), [])
  const settings = useAsync(() => publicSettingsService.get(), [])
  const [f, setF] = useState(empty)
  const [saving, setSaving] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})

  const goTeach = async () => {
    if (!hasRole('Instructor', 'Admin')) await refreshSession()   // nhận vai trò mới vào phiên đăng nhập
    navigate('/giang-day/khoa-hoc')
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const errs: Record<string, string> = {}
    if (f.headline.trim().length < 5) errs.headline = 'Nhập chức danh (ít nhất 5 ký tự)'
    if (f.expertise.trim().length < 3) errs.expertise = 'Nhập lĩnh vực chuyên môn'
    if (f.proposedTopic.trim().length < 10) errs.proposedTopic = 'Mô tả khóa học dự định dạy (ít nhất 10 ký tự)'
    if (f.bio.trim().length < 50) errs.bio = 'Giới thiệu bản thân ít nhất 50 ký tự'
    if (f.portfolioUrl.trim() && !/^https?:\/\//i.test(f.portfolioUrl.trim())) errs.portfolioUrl = 'Đường dẫn bắt đầu bằng http:// hoặc https://'
    setErrors(errs)
    if (Object.keys(errs).length) return
    setSaving(true)
    try {
      await instructorApplicationService.submit({
        headline: f.headline.trim(), bio: f.bio.trim(), expertise: f.expertise.trim(), proposedTopic: f.proposedTopic.trim(),
        portfolioUrl: f.portfolioUrl.trim() || null, yearsOfExperience: f.yearsOfExperience ? Number(f.yearsOfExperience) : null,
      })
      toast.success('Đã gửi hồ sơ. Chúng tôi sẽ phản hồi qua thông báo.')
      setF(empty)
      status.reload()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  const latest = status.data?.latest
  const canApply = status.data && !status.data.isInstructor && latest?.status !== 'Pending'
  const benefits = [
    { icon: Wallet, title: `Hưởng ${settings.data?.instructorSharePercent ?? 70}% doanh thu`, text: 'Theo dõi thu nhập từng đơn và yêu cầu rút tiền về tài khoản ngân hàng.' },
    { icon: Megaphone, title: 'Tiếp cận học viên', text: 'Khóa học được giới thiệu trên trang chủ, danh mục và gợi ý cá nhân hóa.' },
    { icon: BadgeCheck, title: 'Công cụ đầy đủ', text: 'Soạn bài giảng video, bài kiểm tra, trả lời hỏi đáp và đánh giá của học viên.' },
  ]

  return (
    <>
      <PageHeader eyebrow="Giảng dạy" title="Trở thành giảng viên" crumbs={[{ label: 'Trở thành giảng viên' }]}
        description="Chia sẻ kiến thức của bạn với hàng nghìn học viên. Gửi hồ sơ, đội ngũ EduViet xem xét trong 1–3 ngày làm việc." />
      <div className="container-page grid gap-10 py-14 lg:grid-cols-[1fr_1.4fr] lg:items-start">
        <ul className="flex flex-col gap-4">
          {benefits.map(({ icon: Icon, title, text }) => (
            <li key={title} className="card flex gap-4 p-5">
              <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-brand-50 text-brand-600"><Icon className="size-5" aria-hidden="true" /></span>
              <span><span className="block font-semibold">{title}</span><span className="text-sm text-ink-soft">{text}</span></span>
            </li>
          ))}
        </ul>

        <section className="card p-6 sm:p-8" aria-labelledby="apply-title">
          {status.error ? <ErrorState message={status.error} onRetry={status.reload} /> : status.loading ? <Skeleton className="h-96" /> : (
            <>
              {status.data?.isInstructor ? (
                <div className="flex flex-col items-start gap-4">
                  <h2 id="apply-title" className="text-title font-semibold">Bạn đã là giảng viên</h2>
                  <p className="text-ink-soft">Vào khu vực giảng dạy để tạo khóa học đầu tiên và gửi Admin duyệt xuất bản.</p>
                  <button type="button" className="btn-primary" onClick={goTeach}>Vào khu vực giảng dạy</button>
                </div>
              ) : latest && (
                <div className={`mb-6 rounded-(--radius-card) p-4 ${latest.status === 'Rejected' ? 'bg-danger-50' : 'bg-warning-50'}`} role="status">
                  <p className="flex flex-wrap items-center gap-2 font-semibold">
                    <Clock className="size-4" aria-hidden="true" />Hồ sơ gửi lúc {formatDateTime(latest.createdAt)}:
                    <span className={`rounded-(--radius-control) px-2 py-0.5 text-sm ${applicationStatusStyle[latest.status]}`}>{applicationStatusLabel[latest.status]}</span>
                  </p>
                  {latest.status === 'Pending' && <p className="mt-1 text-sm text-ink-soft">Bạn sẽ nhận thông báo khi hồ sơ được xử lý.</p>}
                  {latest.status === 'Rejected' && latest.adminNote && <p className="mt-1 text-sm"><b>Lý do:</b> {latest.adminNote}. Bạn có thể bổ sung và gửi lại.</p>}
                </div>
              )}
              {canApply && (
                <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
                  <h2 id="apply-title" className="text-title font-semibold">Hồ sơ đăng ký</h2>
                  <Field label="Chức danh" placeholder="Ví dụ: Kỹ sư phần mềm 5 năm kinh nghiệm" maxLength={200} value={f.headline} error={errors.headline} onChange={(e) => setF({ ...f, headline: e.target.value })} />
                  <div className="grid gap-4 sm:grid-cols-[1fr_160px]">
                    <Field label="Lĩnh vực chuyên môn" placeholder="Lập trình web, React" maxLength={500} value={f.expertise} error={errors.expertise} onChange={(e) => setF({ ...f, expertise: e.target.value })} />
                    <Field label="Số năm kinh nghiệm" type="number" min={0} max={60} inputMode="numeric" value={f.yearsOfExperience} onChange={(e) => setF({ ...f, yearsOfExperience: e.target.value })} />
                  </div>
                  <Field label="Khóa học dự định dạy" maxLength={500} value={f.proposedTopic} error={errors.proposedTopic} onChange={(e) => setF({ ...f, proposedTopic: e.target.value })} />
                  <TextArea label="Giới thiệu bản thân và kinh nghiệm giảng dạy" rows={5} maxLength={2000} value={f.bio} error={errors.bio} onChange={(e) => setF({ ...f, bio: e.target.value })} />
                  <Field label="Đường dẫn hồ sơ năng lực (không bắt buộc)" type="url" placeholder="https://" value={f.portfolioUrl} error={errors.portfolioUrl}
                    hint="LinkedIn, GitHub, kênh YouTube hoặc video bài giảng mẫu" onChange={(e) => setF({ ...f, portfolioUrl: e.target.value })} />
                  <button type="submit" className="btn-primary self-start" disabled={saving}>{saving && <Spinner className="size-4" />}Gửi hồ sơ</button>
                </form>
              )}
            </>
          )}
        </section>
      </div>
    </>
  )
}
