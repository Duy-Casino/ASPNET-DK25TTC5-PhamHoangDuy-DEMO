import { FlaskConical, Wallet } from 'lucide-react'
import type { PaymentMethod, PaymentProvider } from '../../services/paymentApi'
import { Skeleton } from '../ui'

/** Thông báo bắt buộc khi dùng kênh thử nghiệm: chưa thu tiền thật. */
export function TestModeNotice({ className = '' }: { className?: string }) {
  return (
    <p role="note" className={`flex items-start gap-3 bg-sun-100 p-4 text-sm text-ink ${className}`}>
      <FlaskConical className="mt-0.5 size-5 shrink-0 text-accent-700" aria-hidden="true" />
      <span>
        <b>Hiện tại đang trong giai đoạn test kênh thanh toán, chưa thu tiền thật.</b> Giao dịch sẽ tự động thành công và khóa học
        được mở ngay để bạn trải nghiệm.
      </span>
    </p>
  )
}

/** Danh sách phương thức thanh toán đang bật (lấy từ API), chọn bằng radio. */
export function PaymentMethodPicker({ methods, loading, value, onChange }: {
  methods: PaymentMethod[] | null
  loading: boolean
  value: PaymentProvider | null
  onChange: (p: PaymentProvider) => void
}) {
  if (loading) return <Skeleton className="h-20" />
  if (!methods || methods.length === 0) {
    return <p className="bg-danger-50 p-4 text-sm text-danger">Chưa có phương thức thanh toán nào được bật. Vui lòng liên hệ quản trị viên.</p>
  }

  const selected = methods.find((m) => m.provider === value)
  return (
    <div className="flex flex-col gap-3">
      <fieldset className="flex flex-col gap-2">
        <legend className="sr-only">Phương thức thanh toán</legend>
        {methods.map((m) => (
          <label key={m.provider}
            className={`flex min-h-16 cursor-pointer items-center gap-4 rounded-xl px-4 py-3 transition-colors ${value === m.provider ? 'bg-brand-100' : 'bg-surface-2 hover:bg-surface-3'}`}>
            <input type="radio" name="payment-method" checked={value === m.provider} onChange={() => onChange(m.provider)} className="size-5 accent-brand-600" />
            <span className={`flex size-10 items-center justify-center rounded-lg text-sm font-bold ${m.provider === 'MoMo' ? 'bg-[#a50064] text-white' : m.provider === 'VNPay' ? 'bg-white text-[#005baa]' : 'bg-sun-300 text-ink'}`} aria-hidden="true">
              {m.provider === 'MoMo' ? 'MoMo' : m.provider === 'VNPay' ? 'VN' : <Wallet className="size-5" />}
            </span>
            <span className="flex-1">
              <span className="block font-semibold">{m.displayName}</span>
              {m.isTestMode && <span className="text-sm text-ink-soft">{m.provider === 'Test' ? 'Không thu tiền thật' : 'Môi trường Sandbox — không thu tiền thật'}</span>}
            </span>
          </label>
        ))}
      </fieldset>
      {selected?.isTestMode && <TestModeNotice />}
    </div>
  )
}
