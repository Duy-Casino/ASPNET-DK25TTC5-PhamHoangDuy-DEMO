import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { Avatar, Field, Spinner, TextArea } from '../../components/ui'
import { FileUpload } from '../../components/upload/FileUpload'
import { useDocumentTitle } from '../../hooks/useAsync'
import { useAuth } from '../../hooks/useAuth'
import { accountService } from '../../services/api'
import { getErrorMessage, getFieldErrors } from '../../services/apiClient'
import { formatDate } from '../../utils/format'
import { PASSWORD_HINT, validatePassword } from '../auth/RegisterPage'

const roleLabel: Record<string, string> = { Admin: 'Quản trị viên', Instructor: 'Giảng viên', Student: 'Học viên' }

export default function ProfilePage() {
  useDocumentTitle('Hồ sơ cá nhân')
  const { user } = useAuth()
  if (!user) return null

  return (
    <div className="container-page py-12 md:py-16">
      <div className="flex flex-wrap items-center gap-5">
        <Avatar name={user.fullName} src={user.avatarUrl} size={80} />
        <div>
          <h1 className="text-title font-extrabold">{user.fullName}</h1>
          <p className="text-ink-soft">
            {user.roles.map((r) => roleLabel[r] ?? r).join(', ')} · Tham gia {formatDate(user.createdAt)}
          </p>
        </div>
        <Link to="/tai-khoan/don-hang" className="btn-soft ml-auto">Lịch sử mua hàng</Link>
      </div>
      <div className="mt-12 grid gap-12 lg:grid-cols-2">
        <ProfileForm />
        <PasswordForm />
      </div>
    </div>
  )
}

function ProfileForm() {
  const { user, setUser } = useAuth()
  const [form, setForm] = useState({
    fullName: user?.fullName ?? '',
    phoneNumber: user?.phoneNumber ?? '',
    headline: user?.headline ?? '',
    bio: user?.bio ?? '',
    avatarUrl: user?.avatarUrl ?? '',
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setErrors({})
    try {
      const updated = await accountService.updateProfile({
        fullName: form.fullName,
        phoneNumber: form.phoneNumber || null,
        headline: form.headline || null,
        bio: form.bio || null,
        avatarUrl: form.avatarUrl || null,
      })
      setUser(updated)
      toast.success('Đã cập nhật hồ sơ')
    } catch (err) {
      setErrors(getFieldErrors(err))
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }))

  return (
    <section aria-labelledby="profile-h" className="bg-surface-2 p-6 sm:p-10">
      <h2 id="profile-h" className="text-2xl font-extrabold">Thông tin cá nhân</h2>
      <form onSubmit={submit} className="mt-8 flex flex-col gap-5">
        <Field label="Email" value={user?.email ?? ''} disabled hint="Email dùng để đăng nhập, không thể thay đổi." />
        <Field label="Họ và tên" value={form.fullName} onChange={set('fullName')} error={errors.fullName} autoComplete="name" />
        <Field label="Số điện thoại" type="tel" value={form.phoneNumber} onChange={set('phoneNumber')} error={errors.phoneNumber} autoComplete="tel" />
        <Field label="Chức danh" value={form.headline} onChange={set('headline')} error={errors.headline} placeholder="Ví dụ: Sinh viên CNTT" />
        <div className="flex items-center gap-4">
          <Avatar name={form.fullName || 'U'} src={form.avatarUrl || null} size={64} />
          <FileUpload kind="image" label="Tải ảnh đại diện lên" onUploaded={(r) => r.url && setForm((x) => ({ ...x, avatarUrl: r.url! }))} />
        </div>
        <Field label="Hoặc URL ảnh đại diện" value={form.avatarUrl} onChange={set('avatarUrl')} error={errors.avatarUrl} />
        <TextArea label="Giới thiệu" value={form.bio} onChange={set('bio')} error={errors.bio} maxLength={4000} />
        <button type="submit" className="btn-primary w-fit" disabled={saving}>{saving && <Spinner className="size-4" />}Lưu thay đổi</button>
      </form>
    </section>
  )
}

function PasswordForm() {
  const [form, setForm] = useState({ current: '', next: '', confirm: '' })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const local: Record<string, string> = {}
    if (!form.current) local.current = 'Vui lòng nhập mật khẩu hiện tại.'
    const pw = validatePassword(form.next)
    if (pw) local.next = pw
    if (form.confirm !== form.next) local.confirm = 'Mật khẩu nhập lại không khớp.'
    setErrors(local)
    if (Object.keys(local).length) return

    setSaving(true)
    try {
      await accountService.changePassword(form.current, form.next)
      setForm({ current: '', next: '', confirm: '' })
      toast.success('Đã đổi mật khẩu. Các thiết bị khác đã được đăng xuất.')
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section aria-labelledby="pw-h" className="self-start bg-brand-50 p-6 sm:p-10">
      <h2 id="pw-h" className="text-2xl font-extrabold">Đổi mật khẩu</h2>
      <form onSubmit={submit} className="mt-8 flex flex-col gap-5" noValidate>
        <Field label="Mật khẩu hiện tại" type="password" autoComplete="current-password" value={form.current}
          onChange={(e) => setForm((f) => ({ ...f, current: e.target.value }))} error={errors.current} />
        <Field label="Mật khẩu mới" type="password" autoComplete="new-password" value={form.next}
          onChange={(e) => setForm((f) => ({ ...f, next: e.target.value }))} error={errors.next} hint={PASSWORD_HINT} />
        <Field label="Nhập lại mật khẩu mới" type="password" autoComplete="new-password" value={form.confirm}
          onChange={(e) => setForm((f) => ({ ...f, confirm: e.target.value }))} error={errors.confirm} />
        <button type="submit" className="btn-primary w-fit" disabled={saving}>{saving && <Spinner className="size-4" />}Đổi mật khẩu</button>
      </form>
    </section>
  )
}
