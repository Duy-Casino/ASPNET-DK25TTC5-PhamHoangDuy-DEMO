import { useState } from 'react'
import {
  Award, CalendarCheck, Check, Copy, Flame, Gift, GraduationCap, Share2, ShoppingCart, Sparkles, Ticket, Users, type LucideIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { DataTable, PageHeader, Panel, StatCard } from '../../components/dashboard'
import { ShareDialog } from '../../components/rewards/ShareCourseButton'
import { EmptyState, ErrorState, Pagination, Skeleton, Spinner } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { catalogService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'
import { referralUrl, rewardService, type PointSummary, type PointTransaction, type Redemption } from '../../services/rewardApi'
import { formatDate, formatDateTime, formatNumber, formatPrice } from '../../utils/format'

const taskIcons: Record<string, LucideIcon> = {
  purchase: ShoppingCart, course_completion: GraduationCap, daily_checkin: CalendarCheck, checkin_streak: Flame,
  course_share: Share2, referral: Users,
}
const weekday = new Intl.DateTimeFormat('vi-VN', { weekday: 'short', timeZone: 'UTC' })
const typeLabel: Record<PointTransaction['type'], string> = { Earn: 'Cộng điểm', Spend: 'Đổi điểm', Revoke: 'Thu hồi', Adjust: 'Điều chỉnh' }

export function RewardsPage() {
  useDocumentTitle('Điểm thưởng của tôi')
  const summary = useAsync(() => rewardService.summary(), [], { retries: 1 })
  const [page, setPage] = useState(1)
  const history = useAsync(() => rewardService.transactions(page), [page])
  const vouchers = useAsync(() => rewardService.redemptions(), [])

  const refreshAll = () => { summary.reload(); history.reload(); vouchers.reload() }

  if (summary.error) return <ErrorState message={summary.error} onRetry={summary.reload} />
  const s = summary.data

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Điểm thưởng của tôi" description="Tích điểm khi học, điểm danh, chia sẻ và giới thiệu bạn bè; đổi điểm lấy mã giảm giá." />

      {!s ? (
        <div className="grid gap-4 sm:grid-cols-3">{[0, 1, 2].map((i) => <Skeleton key={i} className="h-28" />)}</div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-3" aria-live="polite">
          <StatCard tone="brand" label="Điểm hiện có" value={formatNumber(s.balance)} sub={!s.redeem.enabled ? undefined : s.balance >= s.redeem.minPoints
            ? `Đổi được tối đa ${formatPrice(Math.floor(s.balance / s.redeem.pointsPerUnit) * s.redeem.valuePerUnit)} giảm giá`
            : `Cần ${formatNumber(s.redeem.minPoints)} điểm để đổi mã giảm giá`} />
          <StatCard label="Tổng điểm đã tích lũy" value={formatNumber(s.totalEarned)} />
          <StatCard label="Tổng điểm đã sử dụng" value={formatNumber(s.totalSpent)} />
        </div>
      )}

      <div className="grid gap-6 xl:grid-cols-[1.05fr_1fr]">
        {s ? <CheckInPanel s={s} onDone={refreshAll} /> : <Skeleton className="h-80" />}
        {s ? <TasksPanel s={s} /> : <Skeleton className="h-80" />}
      </div>

      <div className="grid gap-6 xl:grid-cols-2">
        {s ? <SharePanel s={s} /> : <Skeleton className="h-64" />}
        {s ? <RedeemPanel s={s} vouchers={vouchers.data} onDone={refreshAll} /> : <Skeleton className="h-64" />}
      </div>

      <Panel title="Lịch sử cộng/trừ điểm">
        {history.error ? <ErrorState message={history.error} onRetry={history.reload} />
          : !history.data ? <Skeleton className="h-48" />
          : history.data.items.length === 0 ? (
            <EmptyState title="Chưa có giao dịch điểm">Điểm danh hôm nay để nhận điểm đầu tiên.</EmptyState>
          ) : (
            <>
              <DataTable minWidth={640} head={<><th>Thời gian</th><th>Nội dung</th><th>Loại</th><th className="text-right">Điểm</th><th className="text-right">Số dư</th></>}>
                {history.data.items.map((t) => (
                  <tr key={t.id}>
                    <td className="whitespace-nowrap text-ink-soft">{formatDateTime(t.createdAt)}</td>
                    <td>{t.description}</td>
                    <td className="whitespace-nowrap text-ink-soft">{typeLabel[t.type]}</td>
                    <td className={`text-right font-bold tabular-nums ${t.points > 0 ? 'text-success' : 'text-danger'}`}>{t.points > 0 ? `+${t.points}` : t.points}</td>
                    <td className="text-right tabular-nums">{formatNumber(t.balanceAfter)}</td>
                  </tr>
                ))}
              </DataTable>
              <div className="mt-4"><Pagination page={page} totalPages={history.data.totalPages} onChange={setPage} /></div>
            </>
          )}
      </Panel>
    </div>
  )
}

/* ------------------------------------------------------------------ */
function CheckInPanel({ s, onDone }: { s: PointSummary; onDone: () => void }) {
  const c = s.checkIn
  const [busy, setBusy] = useState(false)
  const target = c.streakTarget || 7
  // Vị trí trong chu kỳ: đã đủ một vòng thì hiện đầy
  const inCycle = c.currentStreak === 0 ? 0 : c.currentStreak % target === 0 ? target : c.currentStreak % target
  const left = target - inCycle

  const checkIn = async () => {
    setBusy(true)
    try {
      const r = await rewardService.checkIn()
      toast.success(r.streakBonus > 0
        ? `Điểm danh +${r.pointsAwarded} và thưởng chuỗi ${r.streak} ngày +${r.streakBonus} điểm!`
        : `Điểm danh thành công +${r.pointsAwarded} điểm (chuỗi ${r.streak} ngày)`)
      onDone()
    } catch (err) {
      toast.error(getErrorMessage(err))
      onDone()
    } finally {
      setBusy(false)
    }
  }

  return (
    <Panel title="Điểm danh hằng ngày">
      <div className="flex flex-col gap-5">
        <div className="flex items-center gap-4">
          <span className="flex size-14 shrink-0 items-center justify-center rounded-2xl bg-accent-50 text-accent-700"><Flame className="size-7" aria-hidden="true" /></span>
          <div className="min-w-0 flex-1">
            <p className="font-display text-2xl font-semibold">Chuỗi {c.currentStreak} ngày</p>
            <p className="text-sm text-ink-soft">
              {!c.streakBonusEnabled
                ? 'Bỏ lỡ một ngày thì chuỗi bắt đầu lại.'
                : inCycle === target && c.checkedInToday
                  ? `Bạn vừa hoàn thành chuỗi ${target} ngày và nhận thêm ${c.streakBonus} điểm!`
                  : `Còn ${left} ngày nữa nhận thêm ${c.streakBonus} điểm. Bỏ lỡ một ngày, chuỗi bắt đầu lại.`}
            </p>
          </div>
        </div>
        <button type="button" className="btn-primary min-h-12 w-full text-base" onClick={checkIn} disabled={busy || c.checkedInToday || !c.enabled}>
            {busy ? <Spinner className="size-4" /> : c.checkedInToday ? <Check className="size-4" aria-hidden="true" /> : <CalendarCheck className="size-4" aria-hidden="true" />}
            {!c.enabled ? 'Tạm dừng' : c.checkedInToday ? 'Đã điểm danh hôm nay' : `Điểm danh +${c.dailyPoints}`}
        </button>

        {/* Tiến độ chuỗi trong chu kỳ */}
        <div>
          <div className="mb-2 flex justify-between text-xs text-ink-muted"><span>Tiến độ chuỗi</span><span>{inCycle}/{target} ngày</span></div>
          <ol className="grid gap-1.5" style={{ gridTemplateColumns: `repeat(${target}, minmax(0, 1fr))` }} aria-label={`Đã điểm danh ${inCycle} trên ${target} ngày của chuỗi`}>
            {Array.from({ length: target }, (_, i) => (
              <li key={i} className={`h-2.5 rounded-full ${i < inCycle ? 'bg-accent-500' : 'bg-surface-3'}`} />
            ))}
          </ol>
        </div>

        {/* 7 ngày gần nhất */}
        <ol className="grid grid-cols-7 gap-2" aria-label="7 ngày gần nhất">
          {c.recentDays.map((d) => {
            const date = new Date(`${d.date}T00:00:00Z`)
            return (
              <li key={d.date} className={`flex flex-col items-center gap-1 rounded-xl border py-2 text-xs ${d.isToday ? 'border-brand-300 bg-brand-50' : 'border-line/70'}`}>
                <span className="text-ink-muted capitalize">{weekday.format(date)}</span>
                <span className="font-semibold">{date.getUTCDate()}</span>
                <span className={`flex size-6 items-center justify-center rounded-full ${d.checked ? 'bg-success text-white' : 'bg-surface-3 text-transparent'}`}>
                  <Check className="size-3.5" aria-hidden="true" />
                </span>
                <span className="sr-only">{d.checked ? 'đã điểm danh' : 'chưa điểm danh'}</span>
              </li>
            )
          })}
        </ol>
      </div>
    </Panel>
  )
}

function TasksPanel({ s }: { s: PointSummary }) {
  return (
    <Panel title="Nhiệm vụ nhận điểm">
      <ul className="flex flex-col divide-y divide-line/70">
        {s.tasks.map((t) => {
          const Icon = taskIcons[t.key] ?? Sparkles
          return (
            <li key={t.key} className={`flex items-center gap-3 py-3 ${t.enabled ? '' : 'opacity-60'}`}>
              <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-brand-50 text-brand-600"><Icon className="size-5" aria-hidden="true" /></span>
              <span className="min-w-0 flex-1">
                <span className="block font-semibold">{t.name}</span>
                <span className="block text-sm text-ink-soft">{t.condition}</span>
              </span>
              <span className="flex shrink-0 flex-col items-end gap-1">
                <span className="font-display font-semibold text-brand-700">+{t.points}</span>
                {!t.enabled ? <span className="text-xs text-ink-muted">Tạm dừng</span>
                  : t.doneToday ? <span className="inline-flex items-center gap-1 text-xs font-semibold text-success"><Check className="size-3" aria-hidden="true" />Hôm nay</span>
                  : null}
              </span>
            </li>
          )
        })}
      </ul>
    </Panel>
  )
}

function SharePanel({ s }: { s: PointSummary }) {
  const courses = useAsync(() => catalogService.search({ pageSize: 30, sort: 'popular' }), [])
  const [courseId, setCourseId] = useState('')
  const [sharing, setSharing] = useState<{ id: string; title: string } | null>(null)
  const [copied, setCopied] = useState(false)
  const shareTask = s.tasks.find((t) => t.key === 'course_share')
  const link = referralUrl(s.referral.code)

  const copyReferral = async () => {
    await navigator.clipboard.writeText(link)
    setCopied(true)
    toast.success('Đã sao chép liên kết giới thiệu')
    setTimeout(() => setCopied(false), 2500)
  }

  return (
    <Panel title="Chia sẻ & giới thiệu">
      <div className="flex flex-col gap-6">
        <div>
          <p className="text-sm text-ink-soft">
            Chọn khóa học để chia sẻ qua Facebook, Zalo hoặc sao chép liên kết.
            {shareTask?.enabled && <> Nhận <b className="text-ink">+{shareTask.points} điểm</b> khi có người mở liên kết (tối đa 1 lần/ngày).</>}
          </p>
          <div className="mt-3 flex flex-col gap-2 sm:flex-row">
            <label htmlFor="share-course" className="sr-only">Khóa học muốn chia sẻ</label>
            <select id="share-course" value={courseId} onChange={(e) => setCourseId(e.target.value)} className="input flex-1" disabled={!courses.data}>
              <option value="">{courses.data ? 'Chọn khóa học…' : 'Đang tải khóa học…'}</option>
              {courses.data?.items.map((c) => <option key={c.id} value={c.id}>{c.title}</option>)}
            </select>
            <button type="button" className="btn-primary min-h-12" disabled={!courseId}
              onClick={() => { const c = courses.data?.items.find((x) => x.id === courseId); if (c) setSharing({ id: c.id, title: c.title }) }}>
              <Share2 className="size-4" aria-hidden="true" /> Chia sẻ khóa học
            </button>
          </div>
        </div>

        <div className="rounded-xl bg-surface-2 p-4">
          <p className="flex items-center gap-2 font-semibold"><Users className="size-4 text-brand-600" aria-hidden="true" /> Giới thiệu bạn bè</p>
          <p className="mt-1 text-sm text-ink-soft">
            {s.referral.enabled ? <>Bạn bè đăng ký bằng mã của bạn và mua khóa học thành công, bạn nhận <b className="text-ink">+{s.referral.pointsPerReferral} điểm</b>.</> : 'Thưởng giới thiệu đang tạm dừng.'}
          </p>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <span className="rounded-lg border-2 border-dashed border-brand-200 bg-white px-3 py-1.5 font-display text-lg font-semibold tracking-[0.2em]">{s.referral.code}</span>
            <button type="button" className="btn-soft" onClick={copyReferral}>
              {copied ? <Check className="size-4" aria-hidden="true" /> : <Copy className="size-4" aria-hidden="true" />} Sao chép liên kết mời
            </button>
          </div>
          <p className="mt-3 text-sm text-ink-muted">Đã giới thiệu {s.referral.referredCount} người · {s.referral.rewardedCount} người đã mua khóa học</p>
        </div>
      </div>
      {sharing && <ShareDialog course={sharing} onClose={() => setSharing(null)} />}
    </Panel>
  )
}

function RedeemPanel({ s, vouchers, onDone }: { s: PointSummary; vouchers: Redemption[] | null; onDone: () => void }) {
  const [busy, setBusy] = useState<number | null>(null)
  const r = s.redeem
  const now = Date.now()

  const redeem = async (points: number) => {
    const value = (points / r.pointsPerUnit) * r.valuePerUnit
    if (!window.confirm(`Đổi ${points} điểm lấy mã giảm ${formatPrice(value)}?`)) return
    setBusy(points)
    try {
      const v = await rewardService.redeem(points)
      toast.success(`Đổi điểm thành công! Mã của bạn: ${v.couponCode}`)
      onDone()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setBusy(null)
    }
  }

  return (
    <Panel title="Đổi điểm lấy mã giảm giá">
      {!r.enabled ? <p className="text-ink-soft">Đổi điểm đang tạm dừng.</p> : (
        <div className="flex flex-col gap-5">
          <p className="text-sm text-ink-soft">{r.pointsPerUnit} điểm = {formatPrice(r.valuePerUnit)}. Mã chỉ dùng được bởi chính bạn, một lần, trong 90 ngày.</p>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {r.options.map((p) => {
              const value = (p / r.pointsPerUnit) * r.valuePerUnit
              const enough = s.balance >= p
              return (
                <button key={p} type="button" disabled={!enough || busy !== null} onClick={() => redeem(p)}
                  className="card card-hover flex flex-col items-center gap-1 p-4 text-center disabled:pointer-events-none disabled:opacity-50">
                  {busy === p ? <Spinner className="size-5 text-brand-600" /> : <Ticket className="size-5 text-accent-700" aria-hidden="true" />}
                  <span className="font-display text-lg font-semibold">{formatPrice(value)}</span>
                  <span className="text-xs text-ink-soft">{formatNumber(p)} điểm</span>
                </button>
              )
            })}
          </div>
          {vouchers && vouchers.length > 0 && (
            <div>
              <p className="mb-2 text-sm font-semibold">Mã đã đổi</p>
              <ul className="flex flex-col gap-2">
                {vouchers.map((v) => {
                  const expired = new Date(v.expiresAt).getTime() < now
                  return (
                    <li key={v.id} className="flex flex-wrap items-center gap-3 rounded-xl border border-line/70 px-3 py-2.5">
                      <Gift className="size-4 text-accent-700" aria-hidden="true" />
                      <span className="font-mono font-semibold tracking-wide">{v.couponCode}</span>
                      <span className="text-sm text-ink-soft">{formatPrice(v.voucherValue)} · HSD {formatDate(v.expiresAt)}</span>
                      <span className={`ml-auto rounded-full px-2.5 py-0.5 text-xs font-semibold ${v.used ? 'bg-surface-3 text-ink-soft' : expired ? 'bg-danger-50 text-danger' : 'bg-success-50 text-success'}`}>
                        {v.used ? 'Đã dùng' : expired ? 'Hết hạn' : 'Còn hiệu lực'}
                      </span>
                      {!v.used && !expired && (
                        <button type="button" className="rounded-lg p-1.5 hover:bg-surface-2" aria-label={`Sao chép mã ${v.couponCode}`}
                          onClick={async () => { await navigator.clipboard.writeText(v.couponCode); toast.success('Đã sao chép mã') }}>
                          <Copy className="size-4" />
                        </button>
                      )}
                    </li>
                  )
                })}
              </ul>
            </div>
          )}
          <p className="flex items-center gap-1.5 text-xs text-ink-muted"><Award className="size-3.5" aria-hidden="true" /> Nhập mã ở bước thanh toán để được giảm giá.</p>
        </div>
      )}
    </Panel>
  )
}
