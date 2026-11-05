import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ArrowDown, ArrowUp, Eye, FileText, Lock, Pencil, PlayCircle, Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { Modal, PageHeader, Panel, SelectField, StatusBadge, Toggle } from '../../components/dashboard'
import { ErrorState, Field, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { catalogService } from '../../services/api'
import { getErrorMessage, getFieldErrors } from '../../services/apiClient'
import { instructorService } from '../../services/dashboardApi'
import type { CourseLevel } from '../../types/common'
import { courseLevelLabel } from '../../types/common'
import type { CourseEdit, LessonEdit, SaveLessonInput, SectionEdit } from '../../types/dashboard'
import { formatClock } from '../../utils/format'
import QuizzesTab from './QuizEditor'
import { FileUpload } from '../../components/upload/FileUpload'

const tabs = [
  { key: 'info', label: 'Thông tin' },
  { key: 'curriculum', label: 'Chương & bài học' },
  { key: 'quizzes', label: 'Bài kiểm tra' },
] as const
type TabKey = (typeof tabs)[number]['key']

export default function CourseEditorPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const { data: course, loading, error, status, reload, setData } = useAsync(() => instructorService.course(id), [id])
  const [tab, setTab] = useState<TabKey>('info')
  const [busy, setBusy] = useState(false)
  useDocumentTitle(course ? `Soạn: ${course.title}` : 'Soạn khóa học')

  if (status === 404) return <ErrorState message="Không tìm thấy khóa học hoặc bạn không có quyền sửa." />
  if (error) return <ErrorState message={error} onRetry={reload} />
  if (loading || !course) return <Skeleton className="h-96" />

  const run = async (fn: () => Promise<unknown>, success: string) => {
    setBusy(true)
    try {
      await fn()
      toast.success(success)
      reload()
    } catch (e) {
      toast.error(getErrorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const remove = async () => {
    if (!window.confirm('Xóa khóa học này? Khóa học đã có học viên sẽ được ẩn thay vì xóa hẳn.')) return
    setBusy(true)
    try {
      await instructorService.deleteCourse(course.id)
      toast.success('Đã xóa khóa học')
      navigate('/giang-day/khoa-hoc')
    } catch (e) {
      toast.error(getErrorMessage(e))
      setBusy(false)
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <Link to="/giang-day/khoa-hoc" className="w-fit text-sm font-semibold text-brand-700 hover:underline">← Danh sách khóa học</Link>
      <PageHeader
        title={course.title}
        actions={
          <>
            <Link to={`/hoc/${course.slug}`} className="btn-soft"><Eye className="size-4" aria-hidden="true" />Xem trước</Link>
            {course.isEditable && (
              <button type="button" className="btn-primary" disabled={busy} onClick={() => run(() => instructorService.submit(course.id), 'Đã gửi khóa học cho quản trị viên duyệt')}>
                {busy && <Spinner className="size-4" />}Gửi duyệt
              </button>
            )}
            {(course.status === 'Published' || course.status === 'Pending') && (
              <button type="button" className="btn-soft" disabled={busy}
                onClick={() => window.confirm('Chuyển về bản nháp để chỉnh sửa? Khóa học sẽ tạm ẩn khỏi trang bán, học viên đã mua vẫn học được.')
                  && run(() => instructorService.unpublish(course.id), 'Đã chuyển về bản nháp')}>
                Chuyển về bản nháp
              </button>
            )}
            <button type="button" className="btn-ghost text-danger" disabled={busy} onClick={remove}><Trash2 className="size-4" aria-hidden="true" />Xóa</button>
          </>
        }
      />
      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={course.status} />
        {course.status === 'Rejected' && course.rejectionReason && <span className="text-sm text-danger">Lý do: {course.rejectionReason}</span>}
      </div>
      {!course.isEditable && (
        <p className="flex items-start gap-3 bg-warning-50 p-4 text-sm">
          <Lock className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          Khóa học đang {course.status === 'Pending' ? 'chờ duyệt' : 'được bán'} nên nội dung bị khóa. Chọn "Chuyển về bản nháp" để chỉnh sửa rồi gửi duyệt lại.
        </p>
      )}

      <div className="flex gap-1 overflow-x-auto rounded-(--radius-control) bg-white p-1" role="tablist">
        {tabs.map((t) => (
          <button key={t.key} type="button" role="tab" aria-selected={tab === t.key} onClick={() => setTab(t.key)}
            className={`flex-1 rounded-(--radius-control) px-4 py-2 font-semibold whitespace-nowrap transition-colors ${tab === t.key ? 'bg-brand-600 text-white' : 'text-ink-soft hover:bg-surface-2'}`}>
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'info' && <InfoTab course={course} onSaved={(c) => setData(c)} />}
      {tab === 'curriculum' && <CurriculumTab course={course} reload={reload} />}
      {tab === 'quizzes' && <QuizzesTab course={course} />}
    </div>
  )
}

const lines = (s: string) => s.split('\n').map((x) => x.trim()).filter(Boolean)

function InfoTab({ course, onSaved }: { course: CourseEdit; onSaved: (c: CourseEdit) => void }) {
  const categories = useAsync(() => catalogService.categories(), [])
  const [f, setF] = useState({
    title: course.title, categoryId: course.categoryId, shortDescription: course.shortDescription ?? '', description: course.description ?? '',
    outcomes: course.outcomes.join('\n'), requirements: course.requirements.join('\n'), thumbnailUrl: course.thumbnailUrl ?? '',
    previewVideoUrl: course.previewVideoUrl ?? '', level: course.level, price: String(course.price), discountPrice: course.discountPrice?.toString() ?? '',
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setF((x) => ({ ...x, [k]: e.target.value }))
  const disabled = !course.isEditable

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setErrors({})
    try {
      const saved = await instructorService.updateCourse(course.id, {
        title: f.title, categoryId: f.categoryId, shortDescription: f.shortDescription || null, description: f.description || null,
        outcomes: lines(f.outcomes), requirements: lines(f.requirements), thumbnailUrl: f.thumbnailUrl || null, previewVideoUrl: f.previewVideoUrl || null,
        language: 'vi', level: f.level, price: Number(f.price) || 0, discountPrice: f.discountPrice ? Number(f.discountPrice) : null,
      })
      onSaved(saved)
      toast.success('Đã lưu thông tin khóa học')
    } catch (err) {
      setErrors(getFieldErrors(err))
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form onSubmit={submit}>
      <fieldset disabled={disabled} className="grid gap-6 lg:grid-cols-[1fr_340px]">
        <Panel title="Nội dung giới thiệu">
          <div className="flex flex-col gap-5">
            <Field label="Tên khóa học" value={f.title} onChange={set('title')} error={errors.title} maxLength={200} />
            <Field label="Mô tả ngắn" value={f.shortDescription} onChange={set('shortDescription')} error={errors.shortDescription} maxLength={500} hint="Một câu hiển thị dưới tên khóa học." />
            <TextArea label="Mô tả chi tiết (cho phép HTML cơ bản: p, ul, li, a, strong)" value={f.description} onChange={set('description')} error={errors.description} rows={8} />
            <TextArea label="Học viên sẽ học được (mỗi dòng một ý)" value={f.outcomes} onChange={set('outcomes')} rows={4} />
            <TextArea label="Yêu cầu (mỗi dòng một ý)" value={f.requirements} onChange={set('requirements')} rows={3} />
          </div>
        </Panel>
        <div className="flex flex-col gap-6">
          <Panel title="Giá & phân loại">
            <div className="flex flex-col gap-5">
              <SelectField label="Danh mục" value={f.categoryId} onChange={(v) => setF((x) => ({ ...x, categoryId: v }))}>
                {categories.data?.flatMap((c) => [c, ...c.children]).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </SelectField>
              <SelectField label="Cấp độ" value={f.level} onChange={(v) => setF((x) => ({ ...x, level: v as CourseLevel }))}>
                {(Object.keys(courseLevelLabel) as CourseLevel[]).map((l) => <option key={l} value={l}>{courseLevelLabel[l]}</option>)}
              </SelectField>
              <Field label="Giá gốc (đồng)" type="number" min={0} step={1000} value={f.price} onChange={set('price')} error={errors.price} />
              <Field label="Giá khuyến mãi (đồng)" type="number" min={0} step={1000} value={f.discountPrice} onChange={set('discountPrice')} error={errors.discountPrice} hint="Để trống nếu không giảm giá." />
            </div>
          </Panel>
          <Panel title="Ảnh & video">
            <div className="flex flex-col gap-5">
              {f.thumbnailUrl && <img src={f.thumbnailUrl} alt="Ảnh đại diện" className="aspect-video w-full rounded-lg object-cover" />}
              {!disabled && <FileUpload kind="image" label={f.thumbnailUrl ? 'Đổi ảnh đại diện' : 'Tải ảnh đại diện lên'} onUploaded={(r) => r.url && setF((x) => ({ ...x, thumbnailUrl: r.url! }))} />}
              <Field label="Hoặc URL ảnh (https)" value={f.thumbnailUrl} onChange={set('thumbnailUrl')} error={errors.thumbnailUrl} />
              <Field label="Video giới thiệu (URL https)" type="url" value={f.previewVideoUrl} onChange={set('previewVideoUrl')} error={errors.previewVideoUrl} />
            </div>
          </Panel>
          {!disabled && <button type="submit" className="btn-primary min-h-12" disabled={saving}>{saving && <Spinner className="size-4" />}Lưu thông tin</button>}
        </div>
      </fieldset>
    </form>
  )
}

function CurriculumTab({ course, reload }: { course: CourseEdit; reload: () => void }) {
  const [newSection, setNewSection] = useState('')
  const [editingSection, setEditingSection] = useState<SectionEdit | null>(null)
  const [lessonModal, setLessonModal] = useState<{ sectionId: string; lesson: LessonEdit | null } | null>(null)
  const [busy, setBusy] = useState(false)
  const editable = course.isEditable

  const act = async (fn: () => Promise<unknown>, msg?: string) => {
    setBusy(true)
    try {
      await fn()
      if (msg) toast.success(msg)
      reload()
    } catch (e) {
      toast.error(getErrorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const move = <T extends { id: string }>(items: T[], index: number, dir: -1 | 1) => {
    const copy = [...items]
    const [item] = copy.splice(index, 1)
    copy.splice(index + dir, 0, item)
    return copy.map((x) => x.id)
  }

  return (
    <div className="flex flex-col gap-4">
      {course.sections.length === 0 && <p className="bg-white p-6 text-ink-soft">Chưa có chương nào. Thêm chương đầu tiên bên dưới.</p>}
      {course.sections.map((s, si) => (
        <Panel key={s.id}>
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="min-w-0 flex-1 text-lg font-bold">Chương {si + 1}: {s.title}</h3>
            {editable && (
              <>
                <IconButton label="Lên" disabled={busy || si === 0} onClick={() => act(() => instructorService.reorderSections(course.id, move(course.sections, si, -1)))}><ArrowUp className="size-4" /></IconButton>
                <IconButton label="Xuống" disabled={busy || si === course.sections.length - 1} onClick={() => act(() => instructorService.reorderSections(course.id, move(course.sections, si, 1)))}><ArrowDown className="size-4" /></IconButton>
                <IconButton label="Sửa chương" onClick={() => setEditingSection(s)}><Pencil className="size-4" /></IconButton>
                <IconButton label="Xóa chương" danger disabled={busy}
                  onClick={() => window.confirm(`Xóa chương "${s.title}" và toàn bộ bài học trong đó?`) && act(() => instructorService.deleteSection(s.id), 'Đã xóa chương')}>
                  <Trash2 className="size-4" />
                </IconButton>
              </>
            )}
          </div>
          <ul className="mt-4 flex flex-col gap-2">
            {s.lessons.map((l, li) => (
              <li key={l.id} className="flex flex-wrap items-center gap-3 rounded-xl bg-surface-2 px-4 py-3">
                {l.type === 'Video' ? <PlayCircle className="size-4 text-ink-muted" aria-hidden="true" /> : <FileText className="size-4 text-ink-muted" aria-hidden="true" />}
                <span className="min-w-0 flex-1 font-medium">{l.title}</span>
                <span className="text-sm text-ink-muted">
                  {formatClock(l.durationSeconds)}{l.isPreview && ' · Học thử'}{!l.isRequired && ' · Không bắt buộc'}{l.resources.length > 0 && ` · ${l.resources.length} tài liệu`}
                </span>
                {editable && (
                  <span className="flex gap-1">
                    <IconButton label="Lên" disabled={busy || li === 0} onClick={() => act(() => instructorService.reorderLessons(s.id, move(s.lessons, li, -1)))}><ArrowUp className="size-4" /></IconButton>
                    <IconButton label="Xuống" disabled={busy || li === s.lessons.length - 1} onClick={() => act(() => instructorService.reorderLessons(s.id, move(s.lessons, li, 1)))}><ArrowDown className="size-4" /></IconButton>
                    <IconButton label="Sửa bài học" onClick={() => setLessonModal({ sectionId: s.id, lesson: l })}><Pencil className="size-4" /></IconButton>
                    <IconButton label="Xóa bài học" danger disabled={busy}
                      onClick={() => window.confirm(`Xóa bài "${l.title}"?`) && act(() => instructorService.deleteLesson(l.id), 'Đã xóa bài học')}>
                      <Trash2 className="size-4" />
                    </IconButton>
                  </span>
                )}
              </li>
            ))}
          </ul>
          {editable && (
            <button type="button" className="btn-soft mt-4" onClick={() => setLessonModal({ sectionId: s.id, lesson: null })}>
              <Plus className="size-4" aria-hidden="true" />Thêm bài học
            </button>
          )}
        </Panel>
      ))}

      {editable && (
        <form className="flex flex-wrap items-end gap-3 rounded-(--radius-card) bg-white p-5"
          onSubmit={(e) => { e.preventDefault(); if (newSection.trim()) void act(async () => { await instructorService.addSection(course.id, newSection.trim()); setNewSection('') }, 'Đã thêm chương') }}>
          <Field label="Chương mới" value={newSection} onChange={(e) => setNewSection(e.target.value)} placeholder="Ví dụ: Làm quen với công cụ" className="min-w-60 flex-1" maxLength={200} />
          <button type="submit" className="btn-primary" disabled={busy || !newSection.trim()}><Plus className="size-4" aria-hidden="true" />Thêm chương</button>
        </form>
      )}

      {editingSection && (
        <SectionModal section={editingSection} onClose={() => setEditingSection(null)} onSaved={() => { setEditingSection(null); reload() }} />
      )}
      {lessonModal && (
        <LessonModal sectionId={lessonModal.sectionId} lesson={lessonModal.lesson} onClose={() => setLessonModal(null)} onSaved={() => { setLessonModal(null); reload() }} />
      )}
    </div>
  )
}

function IconButton({ label, onClick, disabled, danger, children }: { label: string; onClick: () => void; disabled?: boolean; danger?: boolean; children: React.ReactNode }) {
  return (
    <button type="button" aria-label={label} title={label} onClick={onClick} disabled={disabled}
      className={`rounded-lg p-2 transition-colors disabled:opacity-30 ${danger ? 'text-danger hover:bg-danger-50' : 'text-ink-soft hover:bg-surface-3'}`}>
      {children}
    </button>
  )
}

function SectionModal({ section, onClose, onSaved }: { section: SectionEdit; onClose: () => void; onSaved: () => void }) {
  const [title, setTitle] = useState(section.title)
  const [description, setDescription] = useState(section.description ?? '')
  const [saving, setSaving] = useState(false)
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      await instructorService.updateSection(section.id, title, description)
      toast.success('Đã lưu chương')
      onSaved()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal title="Sửa chương" onClose={onClose}>
      <form onSubmit={submit} className="flex flex-col gap-5">
        <Field label="Tên chương" value={title} onChange={(e) => setTitle(e.target.value)} maxLength={200} autoFocus />
        <TextArea label="Mô tả" value={description} onChange={(e) => setDescription(e.target.value)} maxLength={1000} />
        <button type="submit" className="btn-primary" disabled={saving || !title.trim()}>{saving && <Spinner className="size-4" />}Lưu</button>
      </form>
    </Modal>
  )
}

function LessonModal({ sectionId, lesson, onClose, onSaved }: { sectionId: string; lesson: LessonEdit | null; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState<SaveLessonInput>({
    title: lesson?.title ?? '', type: lesson?.type ?? 'Video', content: lesson?.content ?? '', videoUrl: lesson?.videoUrl ?? '',
    videoPublicId: lesson?.videoPublicId ?? null, durationSeconds: lesson?.durationSeconds ?? 0, isPreview: lesson?.isPreview ?? false, isRequired: lesson?.isRequired ?? true,
  })
  const [minutes, setMinutes] = useState(String(Math.round((lesson?.durationSeconds ?? 0) / 60)))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const [resources, setResources] = useState(lesson?.resources ?? [])
  const [res, setRes] = useState({ fileName: '', fileUrl: '' })

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setErrors({})
    const data = { ...f, durationSeconds: Math.round((Number(minutes) || 0) * 60), content: f.content || null, videoUrl: f.type === 'Video' ? f.videoUrl || null : null }
    try {
      if (lesson) await instructorService.updateLesson(lesson.id, data)
      else await instructorService.addLesson(sectionId, data)
      toast.success('Đã lưu bài học')
      onSaved()
    } catch (err) {
      setErrors(getFieldErrors(err))
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  const addResource = async (data?: { fileName: string; fileUrl: string; publicId: string | null; contentType: string | null; fileSize: number }) => {
    if (!lesson) return
    const payload = data ?? { fileName: res.fileName.trim(), fileUrl: res.fileUrl.trim(), publicId: null, contentType: null, fileSize: 0 }
    if (!payload.fileName || (!payload.fileUrl && !payload.publicId)) return
    try {
      const created = await instructorService.addResource(lesson.id, payload)
      setResources((r) => [...r, created])
      if (!data) setRes({ fileName: '', fileUrl: '' })
      toast.success('Đã thêm tài liệu')
    } catch (err) {
      toast.error(getErrorMessage(err))
    }
  }

  const removeResource = async (id: string) => {
    try {
      await instructorService.deleteResource(id)
      setResources((r) => r.filter((x) => x.id !== id))
    } catch (err) {
      toast.error(getErrorMessage(err))
    }
  }

  return (
    <Modal title={lesson ? 'Sửa bài học' : 'Thêm bài học'} onClose={onClose} wide>
      <form onSubmit={submit} className="flex flex-col gap-5">
        <Field label="Tên bài học" value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} error={errors.title} maxLength={200} autoFocus />
        <div className="grid gap-5 sm:grid-cols-2">
          <SelectField label="Loại bài" value={f.type} onChange={(v) => setF({ ...f, type: v as SaveLessonInput['type'] })}>
            <option value="Video">Video</option>
            <option value="Article">Bài viết</option>
          </SelectField>
          <Field label="Thời lượng (phút)" type="number" min={0} value={minutes} onChange={(e) => setMinutes(e.target.value)} error={errors.durationSeconds} />
        </div>
        {f.type === 'Video' && (
          <div className="flex flex-col gap-4 bg-surface-2 p-4">
            {f.videoPublicId ? (
              <p className="flex flex-wrap items-center gap-3 text-sm">
                <span className="font-semibold text-success">Đã có video tải lên (riêng tư, phát qua link có chữ ký).</span>
                <button type="button" className="text-danger underline" onClick={() => setF({ ...f, videoPublicId: null })}>Gỡ video</button>
              </p>
            ) : (
              <Field label="Hoặc dán URL video (https)" type="url" value={f.videoUrl ?? ''} onChange={(e) => setF({ ...f, videoUrl: e.target.value })} error={errors.videoUrl} />
            )}
            <FileUpload kind="video" label={f.videoPublicId ? 'Thay video khác' : 'Tải video lên'}
              onUploaded={(r) => {
                setF((x) => ({ ...x, videoPublicId: r.publicId, videoUrl: null }))
                if (r.durationSeconds) setMinutes(String(Math.max(1, Math.round(r.durationSeconds / 60))))
              }} />
          </div>
        )}
        <TextArea label={f.type === 'Article' ? 'Nội dung bài viết (HTML cơ bản)' : 'Ghi chú bài học (không bắt buộc)'} value={f.content ?? ''}
          onChange={(e) => setF({ ...f, content: e.target.value })} error={errors.content} rows={6} />
        <div className="flex flex-wrap gap-6">
          <Toggle checked={f.isPreview} onChange={(v) => setF({ ...f, isPreview: v })} label="Cho học thử miễn phí" />
          <Toggle checked={f.isRequired} onChange={(v) => setF({ ...f, isRequired: v })} label="Bắt buộc để hoàn thành khóa học" />
        </div>
        <button type="submit" className="btn-primary min-h-12" disabled={saving || !f.title.trim()}>{saving && <Spinner className="size-4" />}Lưu bài học</button>
      </form>

      {lesson && (
        <section className="mt-8 bg-surface-2 p-5" aria-labelledby="res-title">
          <h3 id="res-title" className="font-bold">Tài liệu đính kèm</h3>
          <ul className="mt-3 flex flex-col gap-2">
            {resources.length === 0 && <li className="text-sm text-ink-muted">Chưa có tài liệu.</li>}
            {resources.map((r) => (
              <li key={r.id} className="flex items-center gap-3 rounded-lg bg-white px-3 py-2 text-sm">
                {r.fileUrl.startsWith('private://')
                  ? <span className="flex-1 font-medium">{r.fileName} <span className="text-ink-muted">(riêng tư)</span></span>
                  : <a href={r.fileUrl} target="_blank" rel="noopener noreferrer" className="flex-1 font-medium text-brand-700 hover:underline">{r.fileName}</a>}
                <IconButton label="Xóa tài liệu" danger onClick={() => removeResource(r.id)}><Trash2 className="size-4" /></IconButton>
              </li>
            ))}
          </ul>
          <div className="mt-4">
            <FileUpload kind="document" label="Tải tài liệu lên"
              onUploaded={(r) => void addResource({ fileName: r.fileName, fileUrl: '', publicId: r.publicId, contentType: r.contentType, fileSize: r.size })} />
          </div>
          <details className="mt-4">
            <summary className="cursor-pointer text-sm font-semibold text-brand-700">Hoặc thêm bằng liên kết</summary>
            <div className="mt-3 grid gap-3 sm:grid-cols-[1fr_1.4fr_auto] sm:items-end">
              <Field label="Tên tệp" value={res.fileName} onChange={(e) => setRes({ ...res, fileName: e.target.value })} placeholder="slide-bai-1.pdf" />
              <Field label="URL tệp (https)" type="url" value={res.fileUrl} onChange={(e) => setRes({ ...res, fileUrl: e.target.value })} />
              <button type="button" className="btn-soft" onClick={() => void addResource()} disabled={!res.fileName.trim() || !res.fileUrl.trim()}>Thêm</button>
            </div>
          </details>
        </section>
      )}
    </Modal>
  )
}
