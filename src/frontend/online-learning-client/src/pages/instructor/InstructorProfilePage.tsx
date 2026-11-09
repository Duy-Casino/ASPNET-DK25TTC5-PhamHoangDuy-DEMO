import { useEffect, useState, type FormEvent, type KeyboardEvent } from 'react'
import { Link } from 'react-router-dom'
import { ArrowDown, ArrowUp, Award, BadgeCheck, Briefcase, ExternalLink, GraduationCap, Plus, Trash2, X } from 'lucide-react'
import { toast } from 'sonner'
import { PageHeader, Panel } from '../../components/dashboard'
import { FileUpload } from '../../components/upload/FileUpload'
import { Avatar, ErrorState, Field, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { useAuth } from '../../hooks/useAuth'
import { getErrorMessage, getFieldErrors } from '../../services/apiClient'
import { instructorService } from '../../services/dashboardApi'
import type { ExperienceType, InstructorExperience, InstructorProfileEdit } from '../../types/api'

type ExpDraft = Omit<InstructorExperience, 'id'> & { key: string }

const typeMeta: Record<ExperienceType, { label: string; icon: typeof Briefcase; title: string; org: string }> = {
  Work: { label: 'Kinh nghiệm làm việc', icon: Briefcase, title: 'Vị trí', org: 'Công ty / tổ chức' },
  Education: { label: 'Học vấn', icon: GraduationCap, title: 'Bằng cấp / chuyên ngành', org: 'Trường' },
  Certification: { label: 'Chứng nhận', icon: Award, title: 'Tên chứng nhận', org: 'Đơn vị cấp' },
}

let seq = 0
const newKey = () => `n${++seq}`
const toDraft = (e: Omit<InstructorExperience, 'id'>): ExpDraft => ({ ...e, key: newKey() })

/** Chuẩn hóa khóa lỗi từ API ("experiences[0].Title" → "experiences[0].title"). */
function normalizeErrors(errors: Record<string, string>): Record<string, string> {
  return Object.fromEntries(Object.entries(errors).map(([k, v]) => [k.replace(/\.(\w)/g, (_, c: string) => `.${c.toLowerCase()}`), v]))
}

export function InstructorProfilePage() {
  useDocumentTitle('Hồ sơ giảng viên')
  const { data, error, reload } = useAsync(() => instructorService.profile(), [])
  if (error) return <ErrorState message={error} onRetry={reload} />
  if (!data) return <div className="flex flex-col gap-4"><Skeleton className="h-12 w-72" /><Skeleton className="h-96" /></div>
  return <Editor initial={data} />
}

function Editor({ initial }: { initial: InstructorProfileEdit }) {
  const { user, setUser } = useAuth()
  const [avatarUrl, setAvatarUrl] = useState(initial.avatarUrl ?? '')
  const [headline, setHeadline] = useState(initial.headline ?? '')
  const [bio, setBio] = useState(initial.bio ?? '')
  const [years, setYears] = useState(initial.yearsOfExperience?.toString() ?? '')
  const [expertise, setExpertise] = useState<string[]>(initial.expertise)
  const [tagInput, setTagInput] = useState('')
  const [links, setLinks] = useState({
    websiteUrl: initial.links.website ?? '', linkedInUrl: initial.links.linkedIn ?? '',
    youtubeUrl: initial.links.youtube ?? '', facebookUrl: initial.links.facebook ?? '',
  })
  const [experiences, setExperiences] = useState<ExpDraft[]>(initial.experiences.map(({ id: _id, ...e }) => toDraft(e)))
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const [dirty, setDirty] = useState(false)

  useEffect(() => {
    if (!dirty) return
    const warn = (e: BeforeUnloadEvent) => { e.preventDefault() }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [dirty])

  const touch = <T,>(setter: (v: T) => void) => (v: T) => { setter(v); setDirty(true) }

  const addTag = () => {
    const parts = tagInput.split(/[,;]/).map((t) => t.trim()).filter(Boolean)
    if (parts.length === 0) return
    setExpertise((cur) => {
      const next = [...cur]
      for (const t of parts) if (!next.some((x) => x.toLowerCase() === t.toLowerCase()) && next.length < 15) next.push(t.slice(0, 40))
      return next
    })
    setTagInput('')
    setDirty(true)
  }
  const onTagKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' || e.key === ',') { e.preventDefault(); addTag() }
    if (e.key === 'Backspace' && !tagInput && expertise.length) { setExpertise((c) => c.slice(0, -1)); setDirty(true) }
  }

  const addExperience = (type: ExperienceType) => {
    setExperiences((cur) => [...cur, toDraft({ type, title: '', organization: null, startYear: null, endYear: null, description: null, credentialUrl: null })])
    setDirty(true)
  }
  const updateExp = (key: string, patch: Partial<ExpDraft>) => {
    setExperiences((cur) => cur.map((e) => (e.key === key ? { ...e, ...patch } : e)))
    setDirty(true)
  }
  const moveExp = (key: string, dir: -1 | 1) => {
    setExperiences((cur) => {
      const i = cur.findIndex((e) => e.key === key)
      const j = i + dir
      if (i < 0 || j < 0 || j >= cur.length) return cur
      const next = [...cur]
      ;[next[i], next[j]] = [next[j], next[i]]
      return next
    })
    setDirty(true)
  }
  const removeExp = (key: string) => { setExperiences((cur) => cur.filter((e) => e.key !== key)); setDirty(true) }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setErrors({})
    const yearsNum = years.trim() === '' ? null : Number(years)
    try {
      const saved = await instructorService.updateProfile({
        avatarUrl: avatarUrl.trim() || null,
        headline: headline.trim() || null,
        bio: bio.trim() || null,
        yearsOfExperience: Number.isFinite(yearsNum) ? yearsNum : null,
        expertise,
        websiteUrl: links.websiteUrl.trim() || null,
        linkedInUrl: links.linkedInUrl.trim() || null,
        youtubeUrl: links.youtubeUrl.trim() || null,
        facebookUrl: links.facebookUrl.trim() || null,
        experiences: experiences.map(({ key: _key, ...x }) => ({
          ...x,
          title: x.title.trim(),
          organization: x.organization?.trim() || null,
          description: x.description?.trim() || null,
          credentialUrl: x.credentialUrl?.trim() || null,
        })),
      })
      setExperiences(saved.experiences.map(({ id: _id, ...x }) => toDraft(x)))
      setExpertise(saved.expertise)
      if (user) setUser({ ...user, avatarUrl: saved.avatarUrl, headline: saved.headline, bio: saved.bio })
      setDirty(false)
      toast.success('Đã lưu hồ sơ giảng viên')
    } catch (err) {
      const fields = normalizeErrors(getFieldErrors(err))
      setErrors(fields)
      toast.error(Object.keys(fields).length ? 'Vui lòng kiểm tra lại các ô được đánh dấu' : getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  const groups = (['Work', 'Education', 'Certification'] as ExperienceType[]).map((t) => ({
    type: t, items: experiences.map((e, index) => ({ e, index })).filter(({ e }) => e.type === t),
  }))

  return (
    <form onSubmit={submit} className="flex flex-col gap-6" noValidate>
      <PageHeader
        title="Hồ sơ giảng viên"
        description="Thông tin này hiện trên trang giảng viên công khai và giúp học viên tin tưởng khi chọn khóa học."
        actions={
          <>
            {user && <Link to={`/giang-vien/${user.id}`} target="_blank" className="btn-ghost">Xem trang công khai <ExternalLink className="size-4" aria-hidden="true" /></Link>}
            <button type="submit" className="btn-primary" disabled={saving}>{saving && <Spinner className="size-4" />} Lưu hồ sơ</button>
          </>
        }
      />

      <div className={`flex items-start gap-3 rounded-(--radius-card) p-4 ${initial.isVerified ? 'bg-mint-50 text-mint-700' : 'bg-surface-3 text-ink-soft'}`} role="status">
        <BadgeCheck className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
        <p className="text-sm">
          {initial.isVerified
            ? 'Hồ sơ đã được EduViet xác minh. Huy hiệu "Đã xác minh" hiện trên trang giảng viên và khóa học của bạn.'
            : 'Hồ sơ chưa được xác minh. Hoàn thiện thông tin kinh nghiệm, chứng nhận rồi liên hệ quản trị viên EduViet để được xác minh.'}
        </p>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1fr_380px]">
        <div className="flex min-w-0 flex-col gap-6">
          <Panel title="Thông tin cơ bản">
            <div className="flex flex-col gap-5">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
                <Avatar name={initial.fullName} src={avatarUrl || null} size={96} />
                <div className="flex flex-col gap-2">
                  <p className="font-semibold">Ảnh đại diện</p>
                  <p className="text-sm text-ink-muted">Ảnh chân dung rõ mặt, nền đơn giản, tỷ lệ vuông giúp hồ sơ đáng tin hơn.</p>
                  <div className="flex flex-wrap items-center gap-2">
                    <FileUpload kind="image" label="Tải ảnh lên" onUploaded={(r) => r.url && touch(setAvatarUrl)(r.url)} />
                    {avatarUrl && <button type="button" className="btn-ghost text-danger" onClick={() => touch(setAvatarUrl)('')}>Gỡ ảnh</button>}
                  </div>
                  {errors.avatarUrl && <p className="text-sm font-medium text-danger">{errors.avatarUrl}</p>}
                </div>
              </div>
              <Field label="Chức danh / giới thiệu ngắn" value={headline} maxLength={200} onChange={(e) => touch(setHeadline)(e.target.value)}
                placeholder="Ví dụ: Kỹ sư phần mềm, 10 năm kinh nghiệm .NET" error={errors.headline} />
              <TextArea label="Giới thiệu bản thân" value={bio} maxLength={4000} rows={6} onChange={(e) => touch(setBio)(e.target.value)}
                placeholder="Bạn đang làm gì, đã làm những dự án nào, phong cách giảng dạy ra sao…" error={errors.bio} />
              <Field label="Số năm kinh nghiệm" type="number" min={0} max={60} inputMode="numeric" value={years}
                onChange={(e) => touch(setYears)(e.target.value)} error={errors.yearsOfExperience} className="max-w-48" />
              <div className="flex flex-col gap-1.5">
                <label htmlFor="expertise-input" className="text-sm font-semibold">Chuyên môn <span className="font-normal text-ink-muted">(tối đa 15, nhấn Enter để thêm)</span></label>
                <div className="flex min-h-12 flex-wrap items-center gap-2 rounded-(--radius-control) border border-line bg-white px-2 py-1.5 focus-within:border-brand-500 focus-within:ring-4 focus-within:ring-brand-100">
                  {expertise.map((t) => (
                    <span key={t} className="chip gap-1 py-1 pr-1 text-sm">
                      {t}
                      <button type="button" className="rounded-full p-0.5 hover:bg-brand-100" aria-label={`Bỏ ${t}`}
                        onClick={() => { setExpertise((c) => c.filter((x) => x !== t)); setDirty(true) }}>
                        <X className="size-3.5" aria-hidden="true" />
                      </button>
                    </span>
                  ))}
                  <input id="expertise-input" value={tagInput} onChange={(e) => setTagInput(e.target.value)} onKeyDown={onTagKey} onBlur={addTag}
                    disabled={expertise.length >= 15} placeholder={expertise.length ? '' : 'Ví dụ: React, SQL Server'}
                    className="min-w-32 flex-1 bg-transparent px-2 py-1.5 outline-none" />
                </div>
                {errors.expertise && <p className="text-sm font-medium text-danger">{errors.expertise}</p>}
              </div>
            </div>
          </Panel>

          {groups.map(({ type, items }) => {
            const meta = typeMeta[type]
            return (
              <Panel key={type} title={meta.label}
                actions={<button type="button" className="btn-soft" onClick={() => addExperience(type)}><Plus className="size-4" aria-hidden="true" /> Thêm</button>}>
                {items.length === 0 ? (
                  <p className="text-ink-muted">Chưa có mục nào.</p>
                ) : (
                  <ul className="flex flex-col gap-4">
                    {items.map(({ e, index }) => {
                      const err = (f: string) => errors[`experiences[${index}].${f}`]
                      return (
                        <li key={e.key} className="rounded-xl border border-line/80 p-4">
                          <div className="mb-3 flex items-center justify-between gap-2">
                            <span className="flex items-center gap-2 text-sm font-semibold text-ink-soft"><meta.icon className="size-4" aria-hidden="true" />{meta.label}</span>
                            <span className="flex gap-1">
                              <button type="button" className="rounded-lg p-2 hover:bg-surface-2" aria-label="Đưa lên" onClick={() => moveExp(e.key, -1)}><ArrowUp className="size-4" /></button>
                              <button type="button" className="rounded-lg p-2 hover:bg-surface-2" aria-label="Đưa xuống" onClick={() => moveExp(e.key, 1)}><ArrowDown className="size-4" /></button>
                              <button type="button" className="rounded-lg p-2 text-danger hover:bg-danger-50" aria-label="Xóa mục này" onClick={() => removeExp(e.key)}><Trash2 className="size-4" /></button>
                            </span>
                          </div>
                          <div className="grid gap-3 sm:grid-cols-2">
                            <Field label={meta.title} value={e.title} maxLength={200} required onChange={(ev) => updateExp(e.key, { title: ev.target.value })} error={err('title')} />
                            <Field label={meta.org} value={e.organization ?? ''} maxLength={200} onChange={(ev) => updateExp(e.key, { organization: ev.target.value })} error={err('organization')} />
                            <Field label={type === 'Certification' ? 'Năm cấp' : 'Năm bắt đầu'} type="number" inputMode="numeric" value={e.startYear ?? ''}
                              onChange={(ev) => updateExp(e.key, { startYear: ev.target.value ? Number(ev.target.value) : null })} error={err('startYear')} />
                            <Field label={type === 'Certification' ? 'Năm hết hạn (nếu có)' : 'Năm kết thúc (để trống nếu đang làm)'} type="number" inputMode="numeric" value={e.endYear ?? ''}
                              onChange={(ev) => updateExp(e.key, { endYear: ev.target.value ? Number(ev.target.value) : null })} error={err('endYear')} />
                          </div>
                          {type === 'Certification' ? (
                            <Field className="mt-3" label="Đường dẫn xác minh chứng nhận (https)" value={e.credentialUrl ?? ''} placeholder="https://…"
                              onChange={(ev) => updateExp(e.key, { credentialUrl: ev.target.value })} error={err('credentialUrl')} />
                          ) : (
                            <div className="mt-3">
                              <TextArea label="Mô tả công việc / thành tích" value={e.description ?? ''} maxLength={1000} rows={3}
                                onChange={(ev) => updateExp(e.key, { description: ev.target.value })} error={err('description')} />
                            </div>
                          )}
                        </li>
                      )
                    })}
                  </ul>
                )}
              </Panel>
            )
          })}
        </div>

        <div className="flex flex-col gap-6">
          <Panel title="Liên kết">
            <div className="flex flex-col gap-4">
              {([
                ['websiteUrl', 'Website / blog'], ['linkedInUrl', 'LinkedIn'], ['youtubeUrl', 'YouTube'], ['facebookUrl', 'Facebook'],
              ] as const).map(([key, label]) => (
                <Field key={key} label={label} value={links[key]} placeholder="https://…" inputMode="url"
                  onChange={(e) => { setLinks((l) => ({ ...l, [key]: e.target.value })); setDirty(true) }} error={errors[key]} />
              ))}
            </div>
          </Panel>
          <Panel title="Gợi ý để hồ sơ đáng tin hơn">
            <ul className="flex list-disc flex-col gap-2 pl-5 text-sm text-ink-soft">
              <li>Ảnh chân dung thật, rõ mặt.</li>
              <li>Ghi rõ vị trí, đơn vị và thời gian làm việc; mô tả dự án tiêu biểu.</li>
              <li>Thêm chứng nhận kèm đường dẫn xác minh (Microsoft Learn, Credly, Coursera…).</li>
              <li>Trả lời tin nhắn của học viên sớm: tỷ lệ trả lời được hiển thị công khai.</li>
            </ul>
          </Panel>
        </div>
      </div>

      <div className="sticky bottom-4 z-10 flex justify-end">
        <button type="submit" className="btn-primary min-h-12 px-6 shadow-(--shadow-lift)" disabled={saving || !dirty}>
          {saving && <Spinner className="size-4" />} {dirty ? 'Lưu thay đổi' : 'Đã lưu'}
        </button>
      </div>
    </form>
  )
}
