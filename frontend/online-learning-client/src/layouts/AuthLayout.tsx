import { Outlet } from 'react-router-dom'
import { Logo } from '../components/ui'

/** Trang đăng nhập/đăng ký: biểu mẫu bên trái, ảnh bên phải (ẩn trên điện thoại). */
export default function AuthLayout() {
  return (
    <div className="grid min-h-svh lg:grid-cols-[1fr_1.1fr]">
      <div className="flex flex-col px-4 py-8 sm:px-12 lg:px-20">
        <Logo />
        <main id="main" className="flex flex-1 items-center py-12">
          <div className="w-full max-w-md">
            <Outlet />
          </div>
        </main>
      </div>
      <div className="relative hidden overflow-hidden bg-brand-600 lg:block">
        <img
          src="https://picsum.photos/id/1014/1400/1800"
          alt="Người đứng nhìn ra biển sương sớm"
          width={1400}
          height={1800}
          className="drift absolute inset-0 size-full object-cover brightness-[0.72]"
        />
        <div className="absolute inset-x-12 bottom-12 bg-white p-8 text-ink">
          <p className="text-2xl font-bold leading-tight">Một tài khoản cho mọi khóa học, tiến độ và chứng chỉ của bạn.</p>
          <p className="mt-3 text-ink-soft">Học trên điện thoại, máy tính bảng hay máy tính — tiến độ luôn đồng bộ.</p>
        </div>
      </div>
    </div>
  )
}
