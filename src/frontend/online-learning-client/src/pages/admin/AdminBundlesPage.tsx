import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { DataTable, Modal, PageHeader, Panel, Toggle } from '../../components/dashboard'
import { EmptyState, ErrorState, Field, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { catalogService } from '../../services/api'
import { getErrorMessage, getFieldErrors } from '../../services/apiClient'
import { bundleService, type AdminBundle } from '../../services/growthApi'
import { formatPrice } from '../../utils/format'

/** Quản lý combo khóa học: chọn từ 2 đến 10 khóa đang xuất bản, đặt giá thấp hơn tổng giá gốc. */
export default function AdminBundlesPage() {
  useDocumentTitle('Combo khóa học')
  const { data, loading, error, reload } = useAsync(() => bundleService.adminList(), [])
  const [editing, setEditing] = useState<AdminBundle | 'new' | null>(null)

  const remove = async (b: AdminBundle) => {
    if (!window.confirm(`Xóa combo "${b.title}"?`)) return
    try {
      await bundleService.remove(b.id)
      toast.success('Đã xóa combo')
      reload()
    } catch (err) {
      toast.error(getErrorMessage(err))
    }
  }

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Combo khóa học" description="Giá combo được chia cho từng khóa theo tỷ lệ giá gốc, nên thu nhập của giảng viên vẫn tính đúng."
        actions={<button type="button" className="btn-primary" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" />Tạo combo</button>} />
      <Panel>
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-40" /> : data?.length === 0 ? <EmptyState title="Chưa có combo" /> : (
          <DataTable minWidth={900} head={<><th>Combo</th><th>Khóa học</th><th className="text-right">Giá</th><th className="text-right">Đã bán</th><th>Trạng thái</th><th /></>}>
            {data?.map((b) => (
              <tr key={b.id}>
                <td className="font-semibold">{b.isActive ? <Link to={`/combo/${b.slug}`} className="hover:text-brand-700">{b.title}</Link> : b.title}</td>
                <td className="text-ink-soft">{b.courses.map((c) => c.title).join(', ')}</td>
                <td className="text-right tabular-nums"><p className="font-semibold">{formatPrice(b.price)}</p><p className="text-xs text-ink-muted line-through">{formatPrice(b.originalTotal)}</p></td>
                <td className="text-right tabular-nums">{b.orderCount}</td>
                <td>{b.isActive ? <span className="font-semibold text-success">Đang bán</span> : <span className="text-ink-muted">Đã ẩn</span>}</td>
                <td>
                  <div className="flex gap-1">
                    <button type="button" className="btn-ghost min-h-9 px-3" onClick={() => setEditing(b)} aria-label={`Sửa ${b.title}`}><Pencil className="size-4" /></button>
                    <button type="button" className="btn-ghost min-h-9 px-3 text-danger" onClick={() => remove(b)} aria-label={`Xóa ${b.title}`}><Trash2 className="size-4" /></button>
                  </div>
                </td>
              </tr>
            ))}
          </DataTable>
        )}
      </Panel>
      {editing && <BundleModal bundle={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </div>
  )
}

function BundleModal({ bundle, onClose, onSaved }: { bundle: AdminBundle | null; onClose: () => void; onSaved: () => void }) {
  const courses = useAsync(() => catalogService.search({ pageSize: 100, sort: 'popular' }), [])
  const [f, setF] = useState({
    title: bundle?.title ?? '', description: bundle?.description ?? '', price: bundle ? String(bundle.price) : '',
    isActive: bundle?.isActive ?? true, courseIds: bundle?.courses.map((c) => c.id) ?? [] as string[],
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const all = courses.data?.items ?? []
  const original = all.filter((c) => f.courseIds.includes(c.id)).reduce((s, c) => s + c.price, 0)
  const price = Number(f.price) || 0

  const toggle = (id: string) =>
    setF((x) => ({ ...x, courseIds: x.courseIds.includes(id) ? x.courseIds.filter((c) => c !== id) : [...x.courseIds, id] }))

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setErrors({})
    const input = { title: f.title.trim(), description: f.description.trim() || null, thumbnailUrl: null, price, isActive: f.isActive, courseIds: f.courseIds }
    try {
      if (bundle) await bundleService.update(bundle.id, input)
      else await bundleService.create(input)
      toast.success(bundle ? 'Đã lưu combo' : 'Đã tạo combo')
      onSaved()
    } catch (err) {
      const fe = getFieldErrors(err)
      setErrors(fe)
      if (!Object.keys(fe).length) toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={bundle ? 'Sửa combo' : 'Tạo combo'} onClose={onClose} wide>
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <Field label="Tên combo" value={f.title} onChange={(e) => setF({ ...f, title: e.target.value })} maxLength={200} error={errors.title} />
        <TextArea label="Mô tả" rows={2} value={f.description} onChange={(e) => setF({ ...f, description: e.target.value })} maxLength={2000} />
        <fieldset>
          <legend className="text-sm font-semibold">Khóa học trong combo ({f.courseIds.length} đã chọn, từ 2 đến 10)</legend>
          {errors.courseIds && <p className="mt-1 text-sm font-medium text-danger">{errors.courseIds}</p>}
          {courses.loading ? <Skeleton className="mt-2 h-40" /> : (
            <div className="mt-2 grid max-h-64 gap-1 overflow-y-auto rounded-(--radius-control) border border-line p-2 sm:grid-cols-2">
              {all.map((c) => (
                <label key={c.id} className="flex cursor-pointer items-start gap-2 rounded-lg p-2 hover:bg-surface-2">
                  <input type="checkbox" className="mt-1 size-4 accent-brand-600" checked={f.courseIds.includes(c.id)} onChange={() => toggle(c.id)} />
                  <span className="text-sm"><span className="font-medium">{c.title}</span><span className="block text-ink-muted">{formatPrice(c.price)} · {c.instructor.fullName}</span></span>
                </label>
              ))}
            </div>
          )}
        </fieldset>
        <Field label="Giá combo (đ)" type="number" inputMode="numeric" min={1000} step={1000} value={f.price} onChange={(e) => setF({ ...f, price: e.target.value })}
          error={errors.price} hint={original > 0 ? `Tổng giá gốc ${formatPrice(original)}${price > 0 && price < original ? ` · tiết kiệm ${Math.floor((original - price) * 100 / original)}%` : ''}` : undefined} />
        <Toggle checked={f.isActive} onChange={(v) => setF({ ...f, isActive: v })} label="Đang bán" />
        <button type="submit" className="btn-primary" disabled={saving || !f.title.trim()}>{saving && <Spinner className="size-4" />}Lưu</button>
      </form>
    </Modal>
  )
}
