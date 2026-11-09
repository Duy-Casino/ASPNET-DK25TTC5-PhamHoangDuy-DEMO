import { Link, Outlet } from 'react-router-dom'
import { Award, BookOpen, CreditCard, QrCode, ShieldCheck } from 'lucide-react'
import { DecorBlobs, Logo } from '../components/ui'

const perks = [
  { icon: BookOpen, title: 'Tiến độ đồng bộ', text: 'Học trên điện thoại, máy tính bảng hay máy tính, tiếp tục đúng chỗ đã dừng.' },
  { icon: CreditCard, title: 'Thanh toán VNPay · MoMo', text: 'Vào học ngay sau khi thanh toán thành công.' },
  { icon: Award, title: 'Chứng chỉ có mã xác minh', text: 'Hoàn thành khóa học là có chứng chỉ PDF kèm mã QR.' },
]

/** Trang đăng nhập/đăng ký: biểu mẫu bên trái, bảng giới thiệu sáng màu bên phải (ẩn trên điện thoại). */
export default function AuthLayout() {
  return (
    <div className="grid min-h-svh lg:grid-cols-[1fr_1.05fr]">
      <div className="flex flex-col px-4 py-8 sm:px-12 lg:px-20">
        <div className="flex items-center justify-between gap-4">
          <Logo />
          <Link to="/khoa-hoc" className="text-sm font-semibold text-brand-700 hover:underline">Xem khóa học</Link>
        </div>
        <main id="main" className="flex flex-1 items-center py-12">
          <div className="w-full max-w-md">
            <Outlet />
          </div>
        </main>
      </div>
      <aside className="relative isolate hidden overflow-hidden bg-surface-2 lg:flex lg:items-center lg:justify-center lg:p-16" aria-label="Lợi ích khi có tài khoản">
        <DecorBlobs variant="right" />
        <div className="w-full max-w-lg">
          <span className="chip bg-white shadow-(--shadow-soft)"><ShieldCheck className="size-3.5" aria-hidden="true" /> Một tài khoản cho mọi khóa học</span>
          <p className="mt-5 font-display text-4xl leading-tight font-semibold">
            Học, theo dõi tiến độ và nhận <span className="mark-mint">chứng chỉ</span> ở một nơi
          </p>
          <ul className="mt-10 flex flex-col gap-4">
            {perks.map(({ icon: Icon, title, text }) => (
              <li key={title} className="card flex items-start gap-4 p-5">
                <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-brand-50 text-brand-600"><Icon className="size-5" aria-hidden="true" /></span>
                <span>
                  <span className="block font-semibold">{title}</span>
                  <span className="text-ink-soft">{text}</span>
                </span>
              </li>
            ))}
          </ul>
          <div className="card float-slow mt-6 ml-auto flex w-fit items-center gap-3 rounded-full py-2 pr-5 pl-2 shadow-(--shadow-lift)" aria-hidden="true">
            <span className="flex size-9 items-center justify-center rounded-full bg-mint-100 text-mint-700"><QrCode className="size-4" /></span>
            <span className="text-sm font-semibold">Mã chứng chỉ dạng EDV-YYYY-XXXXXXXX</span>
          </div>
        </div>
      </aside>
    </div>
  )
}
