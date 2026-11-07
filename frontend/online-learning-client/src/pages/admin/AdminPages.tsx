import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { CountChart, DataTable, Modal, PageHeader, Panel, RevenueChart, SelectField, StatCard, StatusBadge, Toggle } from '../../components/dashboard'
import { Avatar, EmptyState, ErrorState, Field, OrderStatusBadge, Pagination, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDebounce, useDocumentTitle } from '../../hooks/useAsync'
import { useAuth } from '../../hooks/useAuth'
import { getErrorMessage, getFieldErrors } from '../../services/apiClient'
import { adminService } from '../../services/dashboardApi'
import type { Category } from '../../types/api'
import type { CourseStatus, OrderStatus } from '../../types/common'
import { courseStatusLabel } from '../../types/common'
import type { AdminCourse, AdminUser, Coupon, SaveCouponInput } from '../../types/dashboard'
import { formatDate, formatDateTime, formatNumber, formatPrice } from '../../utils/format'

async function act(fn: () => Promise<unknown>, success: string, after?: () => void) {
  try {
    await fn()
    toast.success(success)
    after?.()
  } catch (e) {
    toast.error(getErrorMessage(e))
  }
}

// =================== Tổng quan ===================
export function AdminOverviewPage() {
  useDocumentTitle('Quản trị')
  const { data, loading, error, reload } = useAsync(() => adminService.overview(), [])
  if (error) return <ErrorState message={error} onRetry={reload} />
  if (loading || !data) return <div className="grid gap-4 lg:grid-cols-4">{Array.from({ length: 8 }, (_, i) => <Skeleton key={i} className="h-28" />)}</div>

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Tổng quan hệ thống"
        actions={data.pendingCourses > 0 && <Link to="/quan-tri/khoa-hoc" className="btn-accent">{data.pendingCourses} khóa học chờ duyệt</Link>} />
      <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        <StatCard label="Doanh thu" value={formatPrice(data.totalRevenue, '0đ')} sub={`30 ngày: ${formatPrice(data.revenueLast30Days, '0đ')}`} tone="brand" />
        <StatCard label="Đơn đã thanh toán" value={formatNumber(data.paidOrders)} sub={`${data.pendingOrders} đơn chờ thanh toán`} />
        <StatCard label="Người dùng" value={formatNumber(data.totalUsers)} sub={`${data.totalStudents} học viên · ${data.totalInstructors} giảng viên · +${data.newUsersLast30Days} trong 30 ngày`} />
        <StatCard label="Khóa học xuất bản" value={formatNumber(data.publishedCourses)} sub={`${data.pendingCourses} chờ duyệt · ${data.draftCourses} nháp · ${data.rejectedCourses} từ chối`} tone="accent" />
        <StatCard label="Lượt ghi danh" value={formatNumber(data.totalEnrollments)} />
        <StatCard label="Chứng chỉ đã cấp" value={formatNumber(data.totalCertificates)} />
      </div>
      <div className="grid gap-6 xl:grid-cols-[1.4fr_1fr]">
        <Panel title="Doanh thu 12 tháng"><RevenueChart data={data.monthly} /></Panel>
        <Panel title="Người dùng mới theo tháng"><CountChart data={data.monthly} dataKey="newUsers" label="Người dùng mới" /></Panel>
      </div>
      <Panel title="Số đơn đã thanh toán theo tháng"><CountChart data={data.monthly} dataKey="orders" label="Đơn hàng" /></Panel>
    </div>
  )
}

// =================== Duyệt khóa học ===================
export function AdminCoursesPage() {
  useDocumentTitle('Duyệt khóa học')
  const [status, setStatus] = useState<CourseStatus | ''>('Pending')
  const [keyword, setKeyword] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebounce(keyword)
  const { data, loading, error, reload } = useAsync(() => adminService.courses({ status: status || undefined, keyword: q || undefined, page }), [status, q, page])
  const [rejecting, setRejecting] = useState<AdminCourse | null>(null)

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Khóa học" description="Khóa học chỉ hiển thị công khai sau khi được duyệt." />
      <Panel>
        <div className="mb-6 flex flex-wrap items-end gap-3">
          <div className="flex flex-wrap gap-2" role="tablist">
            {(['Pending', 'Published', 'Rejected', 'Draft', ''] as const).map((s) => (
              <button key={s} type="button" role="tab" aria-selected={status === s} className={status === s ? 'btn-primary' : 'btn-soft'}
                onClick={() => { setStatus(s); setPage(1) }}>{s ? courseStatusLabel[s] : 'Tất cả'}</button>
            ))}
          </div>
          <Field label="Tìm kiếm" type="search" value={keyword} onChange={(e) => { setKeyword(e.target.value); setPage(1) }} placeholder="Tên khóa học hoặc giảng viên" className="ml-auto min-w-64" />
        </div>
        {error ? <ErrorState message={error} onRetry={reload} />
          : loading ? <Skeleton className="h-48" />
          : data?.items.length === 0 ? <EmptyState title="Không có khóa học">{status === 'Pending' ? 'Không còn khóa học nào chờ duyệt.' : 'Thử bộ lọc khác.'}</EmptyState>
          : (
            <DataTable minWidth={900} head={<><th>Khóa học</th><th>Giảng viên</th><th>Trạng thái</th><th className="text-right">Giá</th><th className="text-right">Học viên</th><th>Gửi duyệt</th><th>Thao tác</th></>}>
              {data?.items.map((c) => (
                <tr key={c.id}>
                  <td>
                    <div className="flex items-center gap-3">
                      {c.thumbnailUrl && <img src={c.thumbnailUrl} alt="" width={96} height={54} className="aspect-video w-24 rounded-md object-cover" />}
                      <div>
                        <Link to={`/hoc/${c.slug}`} className="font-semibold hover:text-brand-700">{c.title}</Link>
                        <p className="text-ink-muted">{c.categoryName} · {c.totalLessons} bài{c.isDeleted && ' · đã xóa'}</p>
                      </div>
                    </div>
                  </td>
                  <td>{c.instructorName}</td>
                  <td><StatusBadge status={c.status} /></td>
                  <td className="text-right tabular-nums">{formatPrice(c.discountPrice ?? c.price)}</td>
                  <td className="text-right tabular-nums">{c.enrollmentCount}</td>
                  <td className="text-ink-soft">{c.submittedAt ? formatDate(c.submittedAt) : '—'}</td>
                  <td>
                    <div className="flex flex-wrap gap-2">
                      {c.status === 'Pending' && (
                        <>
                          <button type="button" className="btn-primary min-h-9 px-3" onClick={() => act(() => adminService.approve(c.id), 'Đã duyệt và xuất bản', reload)}>Duyệt</button>
                          <button type="button" className="btn-ghost min-h-9 px-3 text-danger" onClick={() => setRejecting(c)}>Từ chối</button>
                        </>
                      )}
                      {c.status === 'Published' && (
                        <>
                          <Toggle checked={c.isFeatured} label="Nổi bật" onChange={(v) => act(() => adminService.setFeatured(c.id, v), v ? 'Đã đặt nổi bật' : 'Đã bỏ nổi bật', reload)} />
                          <button type="button" className="btn-ghost min-h-9 px-3 text-danger"
                            onClick={() => window.confirm(`Gỡ "${c.title}" khỏi trang bán?`) && act(() => adminService.unpublish(c.id), 'Đã gỡ khóa học', reload)}>Gỡ</button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </DataTable>
          )}
        <div className="mt-6"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
      {rejecting && <RejectModal course={rejecting} onClose={() => setRejecting(null)} onDone={() => { setRejecting(null); reload() }} />}
    </div>
  )
}

function RejectModal({ course, onClose, onDone }: { course: AdminCourse; onClose: () => void; onDone: () => void }) {
  const [reason, setReason] = useState('')
  const [saving, setSaving] = useState(false)
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    await act(() => adminService.reject(course.id, reason.trim()), 'Đã từ chối và gửi lý do cho giảng viên', onDone)
    setSaving(false)
  }
  return (
    <Modal title={`Từ chối: ${course.title}`} onClose={onClose}>
      <form onSubmit={submit} className="flex flex-col gap-5">
        <TextArea label="Lý do (giảng viên sẽ nhận được)" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={1000} rows={4} autoFocus />
        <button type="submit" className="btn bg-danger text-white hover:bg-danger/90" disabled={saving || !reason.trim()}>{saving && <Spinner className="size-4" />}Từ chối khóa học</button>
      </form>
    </Modal>
  )
}

// =================== Người dùng ===================
const roleNames: Record<string, string> = { Admin: 'Quản trị', Instructor: 'Giảng viên', Student: 'Học viên' }

export function AdminUsersPage() {
  useDocumentTitle('Người dùng')
  const { user: me } = useAuth()
  const [keyword, setKeyword] = useState('')
  const [role, setRole] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebounce(keyword)
  const { data, loading, error, reload, setData } = useAsync(() => adminService.users({ keyword: q || undefined, role: role || undefined, page }), [q, role, page])
  const [editing, setEditing] = useState<AdminUser | null>(null)

  const replace = (u: AdminUser) => data && setData({ ...data, items: data.items.map((x) => (x.id === u.id ? u : x)) })

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Người dùng" description="Phân quyền và khóa/mở khóa tài khoản." />
      <Panel>
        <div className="mb-6 grid gap-4 sm:grid-cols-[1fr_220px]">
          <Field label="Tìm kiếm" type="search" value={keyword} onChange={(e) => { setKeyword(e.target.value); setPage(1) }} placeholder="Tên, email hoặc số điện thoại" />
          <SelectField label="Vai trò" value={role} onChange={(v) => { setRole(v); setPage(1) }}>
            <option value="">Tất cả</option>
            {Object.entries(roleNames).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </SelectField>
        </div>
        {error ? <ErrorState message={error} onRetry={reload} />
          : loading ? <Skeleton className="h-48" />
          : (
            <DataTable minWidth={980} head={<><th>Người dùng</th><th>Vai trò</th><th className="text-right">Khóa đã mua</th><th className="text-right">Khóa giảng dạy</th><th>Tham gia</th><th>Trạng thái</th><th>Xác minh GV</th><th /></>}>
              {data?.items.map((u) => (
                <tr key={u.id}>
                  <td>
                    <div className="flex items-center gap-3">
                      <Avatar name={u.fullName} src={u.avatarUrl} size={36} />
                      <div><p className="font-semibold">{u.fullName}</p><p className="text-ink-muted">{u.email}</p></div>
                    </div>
                  </td>
                  <td>{u.roles.map((r) => roleNames[r] ?? r).join(', ')}</td>
                  <td className="text-right tabular-nums">{u.enrollmentCount}</td>
                  <td className="text-right tabular-nums">{u.courseCount}</td>
                  <td className="text-ink-soft">{formatDate(u.createdAt)}</td>
                  <td>
                    <Toggle checked={u.isActive} label={u.isActive ? 'Hoạt động' : 'Đã khóa'}
                      onChange={(v) => u.id === me?.id ? toast.error('Không thể tự khóa tài khoản của mình') :
                        act(async () => replace(await adminService.setActive(u.id, v)), v ? 'Đã mở khóa tài khoản' : 'Đã khóa tài khoản')} />
                  </td>
                  <td>
                    {u.roles.includes('Instructor') ? (
                      <Toggle checked={u.isVerifiedInstructor} label={u.isVerifiedInstructor ? 'Đã xác minh' : 'Chưa xác minh'}
                        onChange={(v) => act(async () => replace(await adminService.setInstructorVerified(u.id, v)),
                          v ? `Đã xác minh giảng viên ${u.fullName}` : `Đã bỏ xác minh ${u.fullName}`)} />
                    ) : <span className="text-ink-muted">—</span>}
                  </td>
                  <td><button type="button" className="btn-soft min-h-9 px-3" onClick={() => setEditing(u)}>Phân quyền</button></td>
                </tr>
              ))}
            </DataTable>
          )}
        <div className="mt-6"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
      {editing && <RolesModal user={editing} onClose={() => setEditing(null)} onSaved={(u) => { replace(u); setEditing(null) }} />}
    </div>
  )
}

function RolesModal({ user, onClose, onSaved }: { user: AdminUser; onClose: () => void; onSaved: (u: AdminUser) => void }) {
  const [roles, setRoles] = useState<string[]>(user.roles)
  const [saving, setSaving] = useState(false)
  const toggle = (r: string) => setRoles((x) => (x.includes(r) ? x.filter((y) => y !== r) : [...x, r]))
  const submit = async () => {
    setSaving(true)
    try {
      onSaved(await adminService.setRoles(user.id, roles))
      toast.success('Đã cập nhật vai trò')
    } catch (e) {
      toast.error(getErrorMessage(e))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal title={`Vai trò của ${user.fullName}`} onClose={onClose}>
      <div className="flex flex-col gap-4">
        {Object.entries(roleNames).map(([k, v]) => <Toggle key={k} checked={roles.includes(k)} onChange={() => toggle(k)} label={v} />)}
        <button type="button" className="btn-primary mt-4" disabled={saving || roles.length === 0} onClick={submit}>{saving && <Spinner className="size-4" />}Lưu vai trò</button>
      </div>
    </Modal>
  )
}

// =================== Danh mục ===================
export function AdminCategoriesPage() {
  useDocumentTitle('Danh mục')
  const { data, loading, error, reload } = useAsync(() => adminService.categories(), [])
  const [editing, setEditing] = useState<Category | 'new' | null>(null)
  const flat = data?.flatMap((c) => [c, ...c.children.map((ch) => ({ ...ch, name: `— ${ch.name}` }))]) ?? []

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Danh mục" description="Danh mục 2 cấp. Danh mục đang có khóa học chỉ ẩn được, không xóa được."
        actions={<button type="button" className="btn-primary" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" />Thêm danh mục</button>} />
      <Panel>
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-48" /> : (
          <DataTable minWidth={640} head={<><th>Tên</th><th>Slug</th><th className="text-right">Khóa học</th><th className="text-right">Thứ tự</th><th>Hiển thị</th><th /></>}>
            {flat.map((c) => (
              <tr key={c.id}>
                <td className="font-semibold">{c.name}</td>
                <td className="text-ink-soft">{c.slug}</td>
                <td className="text-right tabular-nums">{c.courseCount}</td>
                <td className="text-right tabular-nums">{c.displayOrder}</td>
                <td>{c.isActive ? 'Hiện' : 'Ẩn'}</td>
                <td>
                  <div className="flex gap-1">
                    <button type="button" className="rounded-lg p-2 hover:bg-surface-2" aria-label={`Sửa ${c.name}`} onClick={() => setEditing(c)}><Pencil className="size-4" /></button>
                    <button type="button" className="rounded-lg p-2 text-danger hover:bg-danger-50" aria-label={`Xóa ${c.name}`}
                      onClick={() => window.confirm(`Xóa danh mục "${c.name}"?`) && act(() => adminService.deleteCategory(c.id), 'Đã xóa danh mục', reload)}>
                      <Trash2 className="size-4" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </DataTable>
        )}
      </Panel>
      {editing && <CategoryModal category={editing === 'new' ? null : editing} parents={data ?? []} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </div>
  )
}

function CategoryModal({ category, parents, onClose, onSaved }: { category: Category | null; parents: Category[]; onClose: () => void; onSaved: () => void }) {
  const [f, setF] = useState({
    name: category?.name.replace(/^— /, '') ?? '', slug: category?.slug ?? '', description: category?.description ?? '',
    parentId: category?.parentId ?? '', displayOrder: String(category?.displayOrder ?? 0), isActive: category?.isActive ?? true,
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      await adminService.saveCategory(category?.id ?? null, {
        name: f.name, slug: f.slug || null, description: f.description || null, parentId: f.parentId || null, displayOrder: Number(f.displayOrder) || 0, isActive: f.isActive,
      })
      toast.success('Đã lưu danh mục')
      onSaved()
    } catch (err) {
      setErrors(getFieldErrors(err))
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal title={category ? 'Sửa danh mục' : 'Thêm danh mục'} onClose={onClose}>
      <form onSubmit={submit} className="flex flex-col gap-5">
        <Field label="Tên" value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} error={errors.name} maxLength={100} autoFocus />
        <Field label="Slug" value={f.slug} onChange={(e) => setF({ ...f, slug: e.target.value })} error={errors.slug} hint="Để trống để tự tạo từ tên." />
        <SelectField label="Danh mục cha" value={f.parentId} onChange={(v) => setF({ ...f, parentId: v })}>
          <option value="">Không (cấp 1)</option>
          {parents.filter((p) => p.id !== category?.id).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
        </SelectField>
        <TextArea label="Mô tả" value={f.description} onChange={(e) => setF({ ...f, description: e.target.value })} maxLength={500} rows={2} />
        <Field label="Thứ tự hiển thị" type="number" min={0} value={f.displayOrder} onChange={(e) => setF({ ...f, displayOrder: e.target.value })} />
        <Toggle checked={f.isActive} onChange={(v) => setF({ ...f, isActive: v })} label="Hiển thị công khai" />
        <button type="submit" className="btn-primary" disabled={saving || !f.name.trim()}>{saving && <Spinner className="size-4" />}Lưu</button>
      </form>
    </Modal>
  )
}

// =================== Đơn hàng ===================
export function AdminOrdersPage() {
  useDocumentTitle('Đơn hàng')
  const [status, setStatus] = useState<OrderStatus | ''>('')
  const [keyword, setKeyword] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebounce(keyword)
  const { data, loading, error, reload } = useAsync(() => adminService.orders({ status: status || undefined, keyword: q || undefined, page }), [status, q, page])
  const statuses: { v: OrderStatus | ''; l: string }[] = [
    { v: '', l: 'Tất cả' }, { v: 'Paid', l: 'Đã thanh toán' }, { v: 'Pending', l: 'Chờ thanh toán' }, { v: 'Cancelled', l: 'Đã hủy' }, { v: 'Expired', l: 'Hết hạn' },
  ]
  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Đơn hàng" description="Giao dịch qua cổng thanh toán được bổ sung ở Giai đoạn 5." />
      <Panel>
        <div className="mb-6 flex flex-wrap items-end gap-3">
          <div className="flex flex-wrap gap-2" role="tablist">
            {statuses.map((s) => (
              <button key={s.v} type="button" role="tab" aria-selected={status === s.v} className={status === s.v ? 'btn-primary' : 'btn-soft'}
                onClick={() => { setStatus(s.v); setPage(1) }}>{s.l}</button>
            ))}
          </div>
          <Field label="Tìm kiếm" type="search" value={keyword} onChange={(e) => { setKeyword(e.target.value); setPage(1) }} placeholder="Mã đơn, email, tên" className="ml-auto min-w-64" />
        </div>
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-48" /> : data?.items.length === 0 ? <EmptyState title="Không có đơn hàng" /> : (
          <DataTable minWidth={900} head={<><th>Mã đơn</th><th>Khách hàng</th><th className="text-right">Số khóa</th><th className="text-right">Giảm</th><th className="text-right">Tổng</th><th>Trạng thái</th><th>Ngày đặt</th></>}>
            {data?.items.map((o) => (
              <tr key={o.id}>
                <td className="font-semibold"><Link to={`/don-hang/${o.id}`} className="text-brand-700 hover:underline">{o.orderCode}</Link></td>
                <td><p className="font-medium">{o.userFullName}</p><p className="text-ink-muted">{o.userEmail}</p></td>
                <td className="text-right tabular-nums">{o.itemCount}</td>
                <td className="text-right tabular-nums">{o.discountAmount ? `-${formatPrice(o.discountAmount)}` : '—'}{o.couponCode && <span className="block text-xs text-ink-muted">{o.couponCode}</span>}</td>
                <td className="text-right font-semibold tabular-nums">{formatPrice(o.totalAmount, '0đ')}</td>
                <td><OrderStatusBadge status={o.status} /></td>
                <td className="text-ink-soft">{formatDateTime(o.createdAt)}</td>
              </tr>
            ))}
          </DataTable>
        )}
        <div className="mt-6"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
    </div>
  )
}

// =================== Mã giảm giá ===================
export function AdminCouponsPage() {
  useDocumentTitle('Mã giảm giá')
  const [page, setPage] = useState(1)
  const { data, loading, error, reload } = useAsync(() => adminService.coupons({ page }), [page])
  const [editing, setEditing] = useState<Coupon | 'new' | null>(null)
  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Mã giảm giá" description="Lượt dùng chỉ tính khi đơn hàng được thanh toán."
        actions={<button type="button" className="btn-primary" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" />Tạo mã</button>} />
      <Panel>
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-48" /> : data?.items.length === 0 ? <EmptyState title="Chưa có mã giảm giá" /> : (
          <DataTable minWidth={900} head={<><th>Mã</th><th>Giảm</th><th>Điều kiện</th><th className="text-right">Đã dùng</th><th>Hiệu lực</th><th>Trạng thái</th><th /></>}>
            {data?.items.map((c) => (
              <tr key={c.id}>
                <td><p className="font-bold">{c.code}</p><p className="text-ink-muted">{c.description}</p></td>
                <td className="tabular-nums">{c.discountType === 'Percentage' ? `${c.discountValue}%` : formatPrice(c.discountValue)}{c.maxDiscountAmount && <span className="block text-xs text-ink-muted">tối đa {formatPrice(c.maxDiscountAmount)}</span>}</td>
                <td className="text-ink-soft">{c.minOrderAmount ? `Đơn từ ${formatPrice(c.minOrderAmount)}` : 'Mọi đơn'}{c.courseTitle && <span className="block">Chỉ: {c.courseTitle}</span>}</td>
                <td className="text-right tabular-nums">{c.usedCount}{c.usageLimit ? `/${c.usageLimit}` : ''}</td>
                <td className="text-ink-soft">{formatDate(c.startsAt)} – {formatDate(c.expiresAt)}</td>
                <td>{c.isActive && new Date(c.expiresAt) > new Date() ? <span className="font-semibold text-success">Đang chạy</span> : <span className="text-ink-muted">Tắt / hết hạn</span>}</td>
                <td>
                  <div className="flex gap-1">
                    <button type="button" className="rounded-lg p-2 hover:bg-surface-2" aria-label={`Sửa ${c.code}`} onClick={() => setEditing(c)}><Pencil className="size-4" /></button>
                    <button type="button" className="rounded-lg p-2 text-danger hover:bg-danger-50" aria-label={`Xóa ${c.code}`}
                      onClick={() => window.confirm(`Xóa mã ${c.code}? Mã đã dùng sẽ chỉ bị vô hiệu hóa.`) && act(() => adminService.deleteCoupon(c.id), 'Đã xóa / vô hiệu hóa mã', reload)}>
                      <Trash2 className="size-4" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </DataTable>
        )}
        <div className="mt-6"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
      {editing && <CouponModal coupon={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </div>
  )
}

const toLocalInput = (iso: string) => {
  const d = new Date(iso)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}

function CouponModal({ coupon, onClose, onSaved }: { coupon: Coupon | null; onClose: () => void; onSaved: () => void }) {
  const now = new Date()
  const [f, setF] = useState({
    code: coupon?.code ?? '', description: coupon?.description ?? '', discountType: coupon?.discountType ?? 'Percentage' as SaveCouponInput['discountType'],
    discountValue: String(coupon?.discountValue ?? 10), maxDiscountAmount: coupon?.maxDiscountAmount?.toString() ?? '', minOrderAmount: String(coupon?.minOrderAmount ?? 0),
    usageLimit: coupon?.usageLimit?.toString() ?? '', perUserLimit: String(coupon?.perUserLimit ?? 1),
    startsAt: toLocalInput(coupon?.startsAt ?? now.toISOString()), expiresAt: toLocalInput(coupon?.expiresAt ?? new Date(now.getTime() + 30 * 864e5).toISOString()),
    isActive: coupon?.isActive ?? true,
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value })

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      await adminService.saveCoupon(coupon?.id ?? null, {
        code: f.code, description: f.description || null, discountType: f.discountType, discountValue: Number(f.discountValue),
        maxDiscountAmount: f.maxDiscountAmount ? Number(f.maxDiscountAmount) : null, minOrderAmount: Number(f.minOrderAmount) || 0,
        usageLimit: f.usageLimit ? Number(f.usageLimit) : null, perUserLimit: Number(f.perUserLimit) || 1, courseId: coupon?.courseId ?? null,
        startsAt: new Date(f.startsAt).toISOString(), expiresAt: new Date(f.expiresAt).toISOString(), isActive: f.isActive,
      })
      toast.success('Đã lưu mã giảm giá')
      onSaved()
    } catch (err) {
      setErrors(getFieldErrors(err))
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={coupon ? `Sửa mã ${coupon.code}` : 'Tạo mã giảm giá'} onClose={onClose} wide>
      <form onSubmit={submit} className="grid gap-5 sm:grid-cols-2">
        <Field label="Mã" value={f.code} onChange={set('code')} error={errors.code} className="uppercase" maxLength={50} autoFocus />
        <Field label="Mô tả" value={f.description} onChange={set('description')} maxLength={500} />
        <SelectField label="Loại giảm" value={f.discountType} onChange={(v) => setF({ ...f, discountType: v as SaveCouponInput['discountType'] })}>
          <option value="Percentage">Theo phần trăm</option>
          <option value="FixedAmount">Số tiền cố định</option>
        </SelectField>
        <Field label={f.discountType === 'Percentage' ? 'Phần trăm giảm' : 'Số tiền giảm (đồng)'} type="number" min={0} value={f.discountValue} onChange={set('discountValue')} error={errors.discountValue} />
        <Field label="Giảm tối đa (đồng)" type="number" min={0} value={f.maxDiscountAmount} onChange={set('maxDiscountAmount')} hint="Để trống nếu không giới hạn." />
        <Field label="Đơn tối thiểu (đồng)" type="number" min={0} value={f.minOrderAmount} onChange={set('minOrderAmount')} />
        <Field label="Tổng lượt dùng" type="number" min={1} value={f.usageLimit} onChange={set('usageLimit')} hint="Để trống nếu không giới hạn." />
        <Field label="Lượt / người" type="number" min={1} value={f.perUserLimit} onChange={set('perUserLimit')} />
        <Field label="Bắt đầu" type="datetime-local" value={f.startsAt} onChange={set('startsAt')} />
        <Field label="Hết hạn" type="datetime-local" value={f.expiresAt} onChange={set('expiresAt')} error={errors.expiresAt} />
        <div className="sm:col-span-2"><Toggle checked={f.isActive} onChange={(v) => setF({ ...f, isActive: v })} label="Đang kích hoạt" /></div>
        <button type="submit" className="btn-primary sm:col-span-2" disabled={saving || !f.code.trim()}>{saving && <Spinner className="size-4" />}Lưu mã</button>
      </form>
    </Modal>
  )
}
