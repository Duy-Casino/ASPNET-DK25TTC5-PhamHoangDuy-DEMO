import { Link } from 'react-router-dom'
import { useDocumentTitle } from '../hooks/useAsync'

export default function NotFoundPage() {
  useDocumentTitle('Không tìm thấy trang')
  return (
    <section className="container-page grid min-h-[60vh] items-center gap-10 py-24 md:grid-cols-2">
      <p className="text-[clamp(8rem,20vw,16rem)] leading-none font-extrabold tracking-[-0.05em] text-brand-600">404</p>
      <div>
        <h1 className="text-title font-extrabold">Trang này không tồn tại</h1>
        <p className="mt-4 text-lg text-ink-soft">Có thể đường dẫn đã thay đổi hoặc khóa học không còn được bán.</p>
        <div className="mt-8 flex flex-wrap gap-3">
          <Link to="/" className="btn-primary">Về trang chủ</Link>
          <Link to="/khoa-hoc" className="btn-soft">Xem khóa học</Link>
        </div>
      </div>
    </section>
  )
}
