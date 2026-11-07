import { useState, type FormEvent } from 'react'
import { Download, History, Pencil, Plus, SlidersHorizontal, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { DataTable, Modal, PageHeader, Panel, SelectField, StatCard, Toggle } from '../../components/dashboard'
import { Avatar, EmptyState, ErrorState, Field, Pagination, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDebounce, useDocumentTitle } from '../../hooks/useAsync'
import { getErrorMessage, getFieldErrors } from '../../services/apiClient'
import {
  adminRewardService, type PointRule, type PointTransactionType, type SavePointRuleInput, type TransactionFilter, type UserBalance,
} from '../../services/rewardApi'
import { formatDateTime, formatNumber } from '../../utils/format'

type Tab = 'overview' | 'balances' | 'history' | 'rules'
const tabs: { key: Tab; label: string }[] = [
  { key: 'overview', label: 'Tổng quan' }, { key: 'balances', label: 'Điểm học viên' },
  { key: 'history', label: 'Lịch sử giao dịch' }, { key: 'rules', label: 'Nhiệm vụ & quy tắc' },
]
const typeLabels: Record<PointTransactionType, string> = { Earn: 'Cộng điểm', Spend: 'Đổi điểm', Revoke: 'Thu hồi', Adjust: 'Điều chỉnh' }

export function AdminRewardsPage() {
  useDocumentTitle('Quản lý điểm thưởng')
  const [tab, setTab] = useState<Tab>('overview')
  const [historyUser, setHistoryUser] = useState<UserBalance | null>(null)
  const rules = useAsync(() => adminRewardService.rules(), [])

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Điểm thưởng" description="Theo dõi điểm phát hành, điểm của học viên, lịch sử giao dịch và cấu hình nhiệm vụ." />
      <div className="flex gap-1 overflow-x-auto rounded-(--radius-control) bg-white p-1 shadow-(--shadow-soft)" role="tablist" aria-label="Mục quản lý điểm">
        {tabs.map((t) => (
          <button key={t.key} type="button" role="tab" aria-selected={tab === t.key} onClick={() => setTab(t.key)}
            className={`flex-1 whitespace-nowrap rounded-[8px] px-4 py-2.5 text-sm font-semibold transition-colors ${tab === t.key ? 'bg-brand-600 text-white' : 'text-ink-soft hover:bg-surface-2'}`}>
            {t.label}
          </button>
        ))}
      </div>
      {tab === 'overview' && <OverviewTab />}
      {tab === 'balances' && <BalancesTab rules={rules.data ?? []} onHistory={(u) => { setHistoryUser(u); setTab('history') }} />}
      {tab === 'history' && <HistoryTab rules={rules.data ?? []} user={historyUser} onClearUser={() => setHistoryUser(null)} />}
      {tab === 'rules' && <RulesTab rules={rules} />}
    </div>
  )
}

/* ------------------------------------------------------------------ */
function OverviewTab() {
  const { data, error, reload } = useAsync(() => adminRewardService.overview(), [])
  if (error) return <ErrorState message={error} onRetry={reload} />
  if (!data) return <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">{[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-28" />)}</div>
  const max = Math.max(1, ...data.topRules.map((r) => r.count))
  return (
    <div className="flex flex-col gap-6">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard tone="brand" label="Tổng điểm đã phát hành" value={formatNumber(data.totalIssued)} sub={`${formatNumber(data.issuedLast30Days)} điểm trong 30 ngày`} />
        <StatCard label="Tổng điểm đã sử dụng / thu hồi" value={formatNumber(data.totalSpent)} />
        <StatCard label="Điểm đang lưu hành" value={formatNumber(data.outstandingBalance)} sub={`${formatNumber(data.holders)} học viên có điểm`} />
        <StatCard label="Số giao dịch" value={formatNumber(data.transactionCount)} />
      </div>
      <Panel title="Nhiệm vụ được thực hiện nhiều nhất">
        {data.topRules.length === 0 ? <p className="text-ink-soft">Chưa có điểm nào được phát hành.</p> : (
          <ul className="flex flex-col gap-3">
            {data.topRules.map((r) => (
              <li key={r.ruleKey} className="grid gap-2 sm:grid-cols-[200px_1fr_auto] sm:items-center">
                <span className="font-semibold">{r.ruleName}</span>
                <span className="h-2.5 overflow-hidden rounded-full bg-surface-3" aria-hidden="true">
                  <span className="block h-full rounded-full bg-brand-500" style={{ width: `${(r.count / max) * 100}%` }} />
                </span>
                <span className="text-sm text-ink-soft tabular-nums">{formatNumber(r.count)} lượt · {formatNumber(r.points)} điểm</span>
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </div>
  )
}

/* ------------------------------------------------------------------ */
function BalancesTab({ rules, onHistory }: { rules: PointRule[]; onHistory: (u: UserBalance) => void }) {
  const [keyword, setKeyword] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebounce(keyword)
  const { data, error, reload } = useAsync(() => adminRewardService.balances(q, page), [q, page])
  const [adjusting, setAdjusting] = useState<UserBalance | null>(null)

  return (
    <Panel>
      <div className="mb-4 max-w-md">
        <Field label="Tìm học viên" type="search" value={keyword} placeholder="Tên hoặc email" onChange={(e) => { setKeyword(e.target.value); setPage(1) }} />
      </div>
      {error ? <ErrorState message={error} onRetry={reload} /> : !data ? <Skeleton className="h-64" />
        : data.items.length === 0 ? <EmptyState title="Chưa có học viên nào có điểm" /> : (
          <>
            <DataTable minWidth={820} head={<><th>Học viên</th><th className="text-right">Hiện có</th><th className="text-right">Đã tích lũy</th><th className="text-right">Đã dùng</th><th>Cập nhật</th><th /></>}>
              {data.items.map((u) => (
                <tr key={u.userId}>
                  <td>
                    <div className="flex items-center gap-3">
                      <Avatar name={u.fullName} src={u.avatarUrl} size={36} />
                      <div className="min-w-0"><p className="font-semibold">{u.fullName}</p><p className="truncate text-ink-muted">{u.email}</p></div>
                    </div>
                  </td>
                  <td className="text-right font-bold tabular-nums">{formatNumber(u.balance)}</td>
                  <td className="text-right tabular-nums">{formatNumber(u.totalEarned)}</td>
                  <td className="text-right tabular-nums">{formatNumber(u.totalSpent)}</td>
                  <td className="text-ink-soft">{formatDateTime(u.updatedAt)}</td>
                  <td className="whitespace-nowrap">
                    <button type="button" className="btn-soft min-h-9 px-3" onClick={() => setAdjusting(u)}><SlidersHorizontal className="size-4" aria-hidden="true" /> Điều chỉnh</button>
                    <button type="button" className="btn-ghost ml-1 min-h-9 px-3" onClick={() => onHistory(u)}><History className="size-4" aria-hidden="true" /> Lịch sử</button>
                  </td>
                </tr>
              ))}
            </DataTable>
            <div className="mt-4"><Pagination page={page} totalPages={data.totalPages} onChange={setPage} /></div>
          </>
        )}
      {adjusting && <AdjustModal user={adjusting} rules={rules} onClose={() => setAdjusting(null)} onDone={() => { setAdjusting(null); reload() }} />}
    </Panel>
  )
}

function AdjustModal({ user, rules, onClose, onDone }: { user: UserBalance; rules: PointRule[]; onClose: () => void; onDone: () => void }) {
  const [points, setPoints] = useState('')
  const [reason, setReason] = useState('')
  const [ruleKey, setRuleKey] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const n = Number(points)
    const local: Record<string, string> = {}
    if (!Number.isInteger(n) || n === 0) local.points = 'Nhập số điểm nguyên khác 0 (âm để trừ).'
    else if (n < 0 && -n > user.balance) local.points = `Không thể trừ quá số điểm hiện có (${user.balance}).`
    if (reason.trim().length < 5) local.reason = 'Bắt buộc ghi lý do (ít nhất 5 ký tự).'
    setErrors(local)
    if (Object.keys(local).length) return
    setSaving(true)
    try {
      await adminRewardService.adjust(user.userId, n, reason.trim(), ruleKey || null)
      toast.success(`Đã ${n > 0 ? 'cộng' : 'trừ'} ${Math.abs(n)} điểm cho ${user.fullName}`)
      onDone()
    } catch (err) {
      setErrors(getFieldErrors(err))
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={`Điều chỉnh điểm: ${user.fullName}`} onClose={onClose}>
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        <p className="rounded-(--radius-control) bg-surface-2 px-4 py-3 text-sm">Số dư hiện tại: <b>{formatNumber(user.balance)} điểm</b></p>
        <Field label="Số điểm (+ để cộng, − để trừ)" type="number" inputMode="numeric" value={points} onChange={(e) => setPoints(e.target.value)} error={errors.points} placeholder="Ví dụ: 50 hoặc -20" />
        <SelectField label="Gắn với nhiệm vụ (không bắt buộc)" value={ruleKey} onChange={setRuleKey}>
          <option value="">Không gắn</option>
          {rules.filter((r) => !r.isSystem).map((r) => <option key={r.id} value={r.key}>{r.name}</option>)}
        </SelectField>
        <TextArea label="Lý do (bắt buộc, học viên sẽ thấy)" value={reason} maxLength={300} rows={3} onChange={(e) => setReason(e.target.value)} error={errors.reason} />
        <button type="submit" className="btn-primary" disabled={saving}>{saving && <Spinner className="size-4" />} Xác nhận điều chỉnh</button>
      </form>
    </Modal>
  )
}

/* ------------------------------------------------------------------ */
function HistoryTab({ rules, user, onClearUser }: { rules: PointRule[]; user: UserBalance | null; onClearUser: () => void }) {
  const [keyword, setKeyword] = useState('')
  const [ruleKey, setRuleKey] = useState('')
  const [type, setType] = useState<PointTransactionType | ''>('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const [exporting, setExporting] = useState(false)
  const q = useDebounce(keyword)
  // Ngày chọn theo giờ Việt Nam → mốc UTC
  const filter: TransactionFilter = {
    userId: user?.userId, keyword: q, ruleKey, type,
    from: from ? new Date(`${from}T00:00:00+07:00`).toISOString() : undefined,
    to: to ? new Date(new Date(`${to}T00:00:00+07:00`).getTime() + 86_400_000).toISOString() : undefined,
  }
  const { data, error, reload } = useAsync(() => adminRewardService.transactions({ ...filter, page }), [user?.userId, q, ruleKey, type, from, to, page])

  const exportCsv = async () => {
    setExporting(true)
    try {
      const blob = await adminRewardService.exportCsv(filter)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `bao-cao-diem-thuong-${new Date().toISOString().slice(0, 10)}.csv`
      a.click()
      URL.revokeObjectURL(url)
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setExporting(false)
    }
  }

  const reset = (fn: () => void) => { fn(); setPage(1) }

  return (
    <Panel actions={<button type="button" className="btn-outline" onClick={exportCsv} disabled={exporting}>{exporting ? <Spinner className="size-4" /> : <Download className="size-4" aria-hidden="true" />} Xuất CSV</button>}
      title="Lịch sử cộng/trừ điểm">
      {user && (
        <p className="mb-4 flex flex-wrap items-center gap-2 rounded-(--radius-control) bg-brand-50 px-4 py-2.5 text-sm text-brand-800">
          Đang xem học viên <b>{user.fullName}</b> ({user.email})
          <button type="button" className="ml-auto font-semibold underline" onClick={onClearUser}>Bỏ lọc</button>
        </p>
      )}
      <div className="mb-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
        <Field label="Tìm kiếm" type="search" value={keyword} placeholder="Học viên hoặc nội dung" onChange={(e) => reset(() => setKeyword(e.target.value))} />
        <SelectField label="Nhiệm vụ" value={ruleKey} onChange={(v) => reset(() => setRuleKey(v))}>
          <option value="">Tất cả</option>
          {rules.map((r) => <option key={r.id} value={r.key}>{r.name}</option>)}
        </SelectField>
        <SelectField label="Loại" value={type} onChange={(v) => reset(() => setType(v as PointTransactionType | ''))}>
          <option value="">Tất cả</option>
          {(Object.keys(typeLabels) as PointTransactionType[]).map((t) => <option key={t} value={t}>{typeLabels[t]}</option>)}
        </SelectField>
        <Field label="Từ ngày" type="date" value={from} onChange={(e) => reset(() => setFrom(e.target.value))} />
        <Field label="Đến ngày" type="date" value={to} onChange={(e) => reset(() => setTo(e.target.value))} />
      </div>
      {error ? <ErrorState message={error} onRetry={reload} /> : !data ? <Skeleton className="h-64" />
        : data.items.length === 0 ? <EmptyState title="Không có giao dịch phù hợp" /> : (
          <>
            <DataTable minWidth={980} head={<><th>Thời gian</th><th>Học viên</th><th>Nội dung</th><th>Loại</th><th className="text-right">Điểm</th><th className="text-right">Số dư</th><th>Người thực hiện</th></>}>
              {data.items.map((t) => (
                <tr key={t.id}>
                  <td className="whitespace-nowrap text-ink-soft">{formatDateTime(t.createdAt)}</td>
                  <td><p className="font-semibold">{t.userFullName}</p><p className="text-ink-muted">{t.userEmail}</p></td>
                  <td>{t.description}</td>
                  <td className="whitespace-nowrap">{typeLabels[t.type]}</td>
                  <td className={`text-right font-bold tabular-nums ${t.points > 0 ? 'text-success' : 'text-danger'}`}>{t.points > 0 ? `+${t.points}` : t.points}</td>
                  <td className="text-right tabular-nums">{formatNumber(t.balanceAfter)}</td>
                  <td className="text-ink-soft">{t.createdByName ?? 'Hệ thống'}</td>
                </tr>
              ))}
            </DataTable>
            <div className="mt-4"><Pagination page={page} totalPages={data.totalPages} onChange={setPage} /></div>
          </>
        )}
    </Panel>
  )
}

/* ------------------------------------------------------------------ */
function RulesTab({ rules }: { rules: ReturnType<typeof useAsync<PointRule[]>> }) {
  const [editing, setEditing] = useState<PointRule | 'new' | null>(null)

  const toggle = async (r: PointRule, isActive: boolean) => {
    try {
      await adminRewardService.updateRule(r.id, { ...r, isActive })
      toast.success(`${isActive ? 'Đã bật' : 'Đã tắt'} "${r.name}"`)
      rules.reload()
    } catch (err) {
      toast.error(getErrorMessage(err))
    }
  }
  const remove = async (r: PointRule) => {
    if (!window.confirm(`Xóa nhiệm vụ "${r.name}"? Lịch sử điểm đã phát vẫn được giữ.`)) return
    try {
      await adminRewardService.deleteRule(r.id)
      toast.success('Đã xóa nhiệm vụ')
      rules.reload()
    } catch (err) {
      toast.error(getErrorMessage(err))
    }
  }

  return (
    <Panel title="Nhiệm vụ & quy tắc thưởng"
      actions={<button type="button" className="btn-primary" onClick={() => setEditing('new')}><Plus className="size-4" aria-hidden="true" /> Nhiệm vụ tùy chỉnh</button>}>
      <p className="mb-4 text-sm text-ink-soft">
        Nhiệm vụ hệ thống gắn với hoạt động thật (thanh toán, học tập, điểm danh…) nên chỉ chỉnh được số điểm, tham số và bật/tắt.
        Nhiệm vụ tùy chỉnh dùng làm nhãn khi trao điểm thủ công (ví dụ sự kiện, cuộc thi).
      </p>
      {rules.error ? <ErrorState message={rules.error} onRetry={rules.reload} /> : !rules.data ? <Skeleton className="h-64" /> : (
        <DataTable minWidth={900} head={<><th>Nhiệm vụ</th><th className="text-right">Điểm</th><th>Điều kiện</th><th>Trạng thái</th><th /></>}>
          {rules.data.map((r) => (
            <tr key={r.id}>
              <td><p className="font-semibold">{r.name}</p><p className="font-mono text-xs text-ink-muted">{r.key}{r.isSystem ? ' · hệ thống' : ' · tùy chỉnh'}</p></td>
              <td className="text-right font-bold tabular-nums">{formatNumber(r.points)}</td>
              <td className="max-w-80 text-ink-soft">{r.condition}</td>
              <td><Toggle checked={r.isActive} label={r.isActive ? 'Đang bật' : 'Đang tắt'} onChange={(v) => toggle(r, v)} /></td>
              <td className="whitespace-nowrap">
                <button type="button" className="btn-soft min-h-9 px-3" onClick={() => setEditing(r)}><Pencil className="size-4" aria-hidden="true" /> Sửa</button>
                {!r.isSystem && <button type="button" className="btn-ghost ml-1 min-h-9 px-3 text-danger" onClick={() => remove(r)} aria-label={`Xóa ${r.name}`}><Trash2 className="size-4" /></button>}
              </td>
            </tr>
          ))}
        </DataTable>
      )}
      {editing && <RuleModal rule={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); rules.reload() }} />}
    </Panel>
  )
}

function RuleModal({ rule, onClose, onSaved }: { rule: PointRule | null; onClose: () => void; onSaved: () => void }) {
  const needsAmount = rule?.key === 'purchase' || rule?.key === 'redeem_voucher'
  const needsThreshold = rule?.key === 'checkin_streak' || rule?.key === 'redeem_voucher'
  const [f, setF] = useState({
    key: rule?.key ?? '', name: rule?.name ?? '', description: rule?.description ?? '', points: String(rule?.points ?? 10),
    amountUnit: rule?.amountUnit?.toString() ?? '', threshold: rule?.threshold?.toString() ?? '', isActive: rule?.isActive ?? true,
    sortOrder: String(rule?.sortOrder ?? 20),
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)
  const set = (k: keyof typeof f) => (e: { target: { value: string } }) => setF((x) => ({ ...x, [k]: e.target.value }))

  const labels: Record<string, { points: string; amount?: string; threshold?: string }> = {
    purchase: { points: 'Số điểm cho mỗi đơn vị tiền', amount: 'Đơn vị tiền (đ)' },
    redeem_voucher: { points: 'Số điểm cho mỗi đơn vị đổi', amount: 'Giá trị voucher mỗi đơn vị (đ)', threshold: 'Số điểm tối thiểu mỗi lần đổi' },
    checkin_streak: { points: 'Điểm thưởng khi đủ chuỗi', threshold: 'Số ngày liên tục' },
  }
  const l = labels[rule?.key ?? ''] ?? { points: 'Số điểm' }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setErrors({})
    const data: SavePointRuleInput = {
      key: rule ? undefined : f.key.trim(), name: f.name.trim(), description: f.description.trim() || null,
      points: Number(f.points), amountUnit: f.amountUnit ? Number(f.amountUnit) : null, threshold: f.threshold ? Number(f.threshold) : null,
      isActive: f.isActive, sortOrder: Number(f.sortOrder) || 0,
    }
    try {
      if (rule) await adminRewardService.updateRule(rule.id, data)
      else await adminRewardService.createRule(data)
      toast.success('Đã lưu nhiệm vụ')
      onSaved()
    } catch (err) {
      setErrors(getFieldErrors(err))
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={rule ? `Sửa: ${rule.name}` : 'Thêm nhiệm vụ tùy chỉnh'} onClose={onClose}>
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {!rule && <Field label="Khóa (chữ thường, số, gạch dưới)" value={f.key} onChange={set('key')} placeholder="su_kien_thang_11" error={errors.key} />}
        <Field label="Tên nhiệm vụ" value={f.name} onChange={set('name')} error={errors.name} />
        <TextArea label="Mô tả" value={f.description} rows={2} onChange={set('description')} error={errors.description} />
        <Field label={l.points} type="number" inputMode="numeric" min={0} value={f.points} onChange={set('points')} error={errors.points} />
        {needsAmount && <Field label={l.amount!} type="number" inputMode="numeric" min={1} value={f.amountUnit} onChange={set('amountUnit')} error={errors.amountUnit} />}
        {needsThreshold && <Field label={l.threshold!} type="number" inputMode="numeric" min={1} value={f.threshold} onChange={set('threshold')} error={errors.threshold} />}
        <Field label="Thứ tự hiển thị" type="number" inputMode="numeric" value={f.sortOrder} onChange={set('sortOrder')} />
        <Toggle checked={f.isActive} label={f.isActive ? 'Đang bật' : 'Đang tắt'} onChange={(v) => setF((x) => ({ ...x, isActive: v }))} />
        {rule?.key === 'purchase' && (
          <p className="text-sm text-ink-soft">Điểm = làm tròn xuống (số tiền thanh toán × điểm / đơn vị tiền). Ví dụ 10 điểm/100.000đ: đơn 249.000đ được 24 điểm.</p>
        )}
        <button type="submit" className="btn-primary" disabled={saving}>{saving && <Spinner className="size-4" />} Lưu</button>
      </form>
    </Modal>
  )
}
