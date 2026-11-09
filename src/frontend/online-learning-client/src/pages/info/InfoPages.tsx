import type { ReactNode } from 'react'
import { Link, NavLink, useParams } from 'react-router-dom'
import {
  Award, ChevronDown, Clock, CreditCard, FileText, GraduationCap, Mail, MapPin, Phone, RotateCcw, ShieldCheck, Users,
} from 'lucide-react'
import { PageHeader } from '../../components/ui'
import { site } from '../../config/site'
import { useSeo } from '../../hooks/useSeo'
import NotFoundPage from '../NotFoundPage'

/* ------------------------------------------------------------------ */
/* Giới thiệu                                                           */
/* ------------------------------------------------------------------ */
export function AboutPage() {
  useSeo({ title: 'Giới thiệu', description: `${site.name} là nền tảng khóa học trực tuyến của giảng viên Việt, mỗi chứng chỉ có mã xác minh công khai.` })
  const values: { icon: typeof Award; title: string; text: string }[] = [
    { icon: GraduationCap, title: 'Giảng viên đang làm nghề', text: 'Khóa học do người làm thực tế xây dựng, được đội ngũ EduViet kiểm duyệt trước khi xuất bản.' },
    { icon: ShieldCheck, title: 'Chứng chỉ minh bạch', text: 'Mỗi chứng chỉ có mã riêng và mã QR. Bất kỳ ai cũng kiểm tra được chứng chỉ là thật và còn hiệu lực.' },
    { icon: CreditCard, title: 'Thanh toán an toàn', text: 'Thanh toán qua VNPay và MoMo. EduViet không lưu thông tin thẻ của bạn.' },
    { icon: Users, title: 'Học theo tốc độ riêng', text: 'Tiến độ được lưu trên mọi thiết bị; bài kiểm tra giúp bạn chắc kiến thức trước khi học tiếp.' },
  ]
  return (
    <>
      <PageHeader eyebrow="Về chúng tôi" title={`Về ${site.name}`} crumbs={[{ label: 'Giới thiệu' }]}
        description="Nền tảng khóa học trực tuyến giúp người Việt học kỹ năng thực tế và chứng minh được điều mình đã học." />
      <div className="container-page grid gap-12 py-14 lg:grid-cols-[1.1fr_1fr] lg:items-start">
        <div className="flex flex-col gap-5 text-lg leading-relaxed text-ink-soft">
          <h2 className="text-title font-semibold text-ink">Sứ mệnh</h2>
          <p>
            {site.name} kết nối học viên với giảng viên đang làm nghề trong các lĩnh vực lập trình, thiết kế, dữ liệu, kinh doanh,
            marketing, ngoại ngữ và kỹ năng văn phòng. Mỗi khóa học gồm video, tài liệu và bài kiểm tra; học xong, học viên nhận
            chứng chỉ có mã xác minh mà nhà tuyển dụng có thể tự kiểm tra.
          </p>
          <p>
            Chúng tôi tin rằng học trực tuyến chỉ có giá trị khi kết quả rõ ràng: bạn biết mình học được gì, và người khác tin được
            điều đó. Vì vậy mọi khóa học đều qua kiểm duyệt nội dung, mọi đánh giá đều đến từ học viên đã mua khóa học, và mọi
            chứng chỉ đều có thể tra cứu công khai.
          </p>
          <div className="flex flex-wrap gap-3 pt-2">
            <Link to="/khoa-hoc" className="btn-primary min-h-12 px-6">Khám phá khóa học</Link>
            <Link to="/lien-he" className="btn-outline min-h-12 px-6">Liên hệ với chúng tôi</Link>
          </div>
        </div>
        <ul className="grid gap-4 sm:grid-cols-2">
          {values.map(({ icon: Icon, title, text }) => (
            <li key={title} className="card flex flex-col gap-3 p-6">
              <span className="flex size-11 items-center justify-center rounded-xl bg-brand-50 text-brand-600"><Icon className="size-5" aria-hidden="true" /></span>
              <h3 className="text-heading font-semibold">{title}</h3>
              <p className="text-ink-soft">{text}</p>
            </li>
          ))}
        </ul>
      </div>
    </>
  )
}

/* ------------------------------------------------------------------ */
/* Liên hệ                                                              */
/* ------------------------------------------------------------------ */
const faqs: { q: string; a: ReactNode }[] = [
  { q: 'EduViet hỗ trợ những phương thức thanh toán nào?', a: 'Bạn có thể thanh toán qua VNPay (thẻ ATM nội địa, thẻ quốc tế, QR ngân hàng) hoặc ví MoMo. Khóa học được mở ngay sau khi cổng thanh toán xác nhận giao dịch thành công.' },
  { q: 'Đã thanh toán nhưng chưa vào học được thì làm sao?', a: <>Mở <Link to="/tai-khoan/don-hang" className="font-semibold text-brand-700 hover:underline">Lịch sử mua hàng</Link> để xem trạng thái đơn. Nếu đơn vẫn "Chờ thanh toán" sau 15 phút dù tài khoản đã bị trừ tiền, hãy gửi mã đơn hàng cho chúng tôi qua email.</> },
  { q: 'Làm sao để xác minh một chứng chỉ?', a: <>Nhập mã chứng chỉ (dạng EDV-YYYY-XXXXXXXX) tại trang <Link to="/xac-minh" className="font-semibold text-brand-700 hover:underline">Xác minh chứng chỉ</Link> hoặc quét mã QR in trên chứng chỉ.</> },
  { q: 'Tôi có được hoàn tiền không?', a: <>Có, theo các điều kiện tại <Link to="/chinh-sach/hoan-tien" className="font-semibold text-brand-700 hover:underline">Chính sách hoàn tiền</Link>.</> },
  { q: 'Tôi quên mật khẩu?', a: <>Dùng chức năng <Link to="/quen-mat-khau" className="font-semibold text-brand-700 hover:underline">Quên mật khẩu</Link>; liên kết đặt lại sẽ được gửi tới email đăng ký.</> },
]

export function ContactPage() {
  useSeo({ title: 'Liên hệ', description: `Liên hệ ${site.name}: email ${site.email}, điện thoại ${site.phone}.` })
  const channels = [
    { icon: Mail, label: 'Email', value: site.email, href: `mailto:${site.email}` },
    { icon: Phone, label: 'Điện thoại', value: site.phone, href: site.phoneHref },
    { icon: MapPin, label: 'Địa chỉ', value: site.address },
    { icon: Clock, label: 'Giờ hỗ trợ', value: site.supportHours },
  ]
  return (
    <>
      <PageHeader eyebrow="Hỗ trợ" title="Liên hệ" crumbs={[{ label: 'Liên hệ' }]}
        description="Cần hỗ trợ về khóa học, thanh toán hay chứng chỉ? Gửi email kèm mã đơn hàng (nếu có) để được xử lý nhanh nhất." />
      <div className="container-page grid gap-12 py-14 lg:grid-cols-[1fr_1.2fr] lg:items-start">
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-1">
          {channels.map(({ icon: Icon, label, value, href }) => (
            <li key={label} className="card flex items-center gap-4 p-5">
              <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-brand-50 text-brand-600"><Icon className="size-5" aria-hidden="true" /></span>
              <span className="min-w-0">
                <span className="block text-sm text-ink-muted">{label}</span>
                {href ? <a href={href} className="font-semibold break-words hover:text-brand-700">{value}</a> : <span className="font-semibold">{value}</span>}
              </span>
            </li>
          ))}
        </ul>
        <section aria-labelledby="faq-title">
          <h2 id="faq-title" className="text-title font-semibold">Câu hỏi thường gặp</h2>
          <div className="mt-6 flex flex-col gap-3">
            {faqs.map((f, i) => (
              <details key={f.q} open={i === 0} className="card group overflow-hidden">
                <summary className="flex cursor-pointer list-none items-center gap-3 p-5 font-semibold hover:bg-surface-2 [&::-webkit-details-marker]:hidden">
                  <span className="flex-1">{f.q}</span>
                  <ChevronDown className="size-5 shrink-0 text-ink-muted transition-transform group-open:rotate-180" aria-hidden="true" />
                </summary>
                <div className="px-5 pb-5 text-ink-soft">{f.a}</div>
              </details>
            ))}
          </div>
        </section>
      </div>
    </>
  )
}

/* ------------------------------------------------------------------ */
/* Chính sách                                                           */
/* ------------------------------------------------------------------ */
function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-xl font-semibold text-ink">{title}</h2>
      {children}
    </section>
  )
}

const policies: Record<string, { title: string; icon: typeof FileText; summary: string; body: ReactNode }> = {
  'dieu-khoan': {
    title: 'Điều khoản sử dụng',
    icon: FileText,
    summary: `Quy định khi sử dụng website và các khóa học trên ${site.name}.`,
    body: (
      <>
        <Section title="1. Tài khoản">
          <p>Bạn cần tài khoản để mua và học khóa học. Bạn chịu trách nhiệm giữ bí mật mật khẩu và mọi hoạt động diễn ra dưới tài khoản của mình. Thông tin đăng ký (họ tên, email) cần chính xác vì họ tên sẽ được in trên chứng chỉ.</p>
        </Section>
        <Section title="2. Quyền sử dụng khóa học">
          <p>Khi mua khóa học, bạn được quyền truy cập nội dung khóa học cho mục đích học tập cá nhân. Bạn không được sao chép, ghi lại, phân phối, bán lại hoặc chia sẻ tài khoản và nội dung khóa học cho người khác.</p>
        </Section>
        <Section title="3. Nội dung của giảng viên">
          <p>Giảng viên chịu trách nhiệm về nội dung mình đăng tải và cam kết có quyền sử dụng hợp pháp với nội dung đó. {site.name} kiểm duyệt khóa học trước khi xuất bản và có quyền ẩn khóa học vi phạm.</p>
        </Section>
        <Section title="4. Đánh giá và bình luận">
          <p>Chỉ học viên đã mua khóa học mới được đánh giá. Đánh giá phải trung thực, không chứa nội dung xúc phạm, quảng cáo hay thông tin cá nhân của người khác.</p>
        </Section>
        <Section title="5. Chứng chỉ">
          <p>Chứng chỉ được cấp tự động khi bạn hoàn thành khóa học theo yêu cầu (học hết bài và đạt bài kiểm tra nếu có). {site.name} có quyền thu hồi chứng chỉ khi phát hiện gian lận; chứng chỉ bị thu hồi sẽ hiển thị rõ trạng thái và lý do trên trang xác minh.</p>
        </Section>
        <Section title="6. Hành vi bị cấm">
          <ul className="list-disc space-y-1 pl-6">
            <li>Truy cập trái phép, can thiệp hệ thống, dò quét lỗ hổng khi chưa được cho phép.</li>
            <li>Dùng công cụ tự động tải nội dung khóa học.</li>
            <li>Gian lận trong bài kiểm tra hoặc thanh toán.</li>
          </ul>
          <p>Vi phạm có thể dẫn đến khóa tài khoản mà không hoàn tiền.</p>
        </Section>
        <Section title="7. Thay đổi điều khoản">
          <p>Điều khoản có thể được cập nhật; ngày cập nhật được ghi ở đầu trang. Tiếp tục sử dụng website sau khi cập nhật nghĩa là bạn đồng ý với điều khoản mới.</p>
        </Section>
      </>
    ),
  },
  'hoan-tien': {
    title: 'Chính sách hoàn tiền',
    icon: RotateCcw,
    summary: 'Điều kiện và cách thức yêu cầu hoàn tiền khóa học.',
    body: (
      <>
        <Section title="1. Điều kiện hoàn tiền">
          <p>Bạn có thể yêu cầu hoàn tiền một khóa học khi đáp ứng đồng thời:</p>
          <ul className="list-disc space-y-1 pl-6">
            <li>Yêu cầu được gửi trong vòng <b className="text-ink">{site.refundDays} ngày</b> kể từ ngày thanh toán thành công.</li>
            <li>Bạn đã học không quá <b className="text-ink">{site.refundMaxProgressPercent}%</b> nội dung khóa học.</li>
            <li>Chưa được cấp chứng chỉ cho khóa học đó.</li>
          </ul>
        </Section>
        <Section title="2. Trường hợp không hoàn tiền">
          <ul className="list-disc space-y-1 pl-6">
            <li>Khóa học miễn phí hoặc mua bằng mã giảm giá 100%.</li>
            <li>Tài khoản bị khóa do vi phạm điều khoản sử dụng.</li>
          </ul>
        </Section>
        <Section title="3. Cách gửi yêu cầu">
          <p>Gửi email tới <a href={`mailto:${site.email}`} className="font-semibold text-brand-700 hover:underline">{site.email}</a> từ địa chỉ email đăng ký tài khoản, kèm <b className="text-ink">mã đơn hàng</b> (xem tại <Link to="/tai-khoan/don-hang" className="font-semibold text-brand-700 hover:underline">Lịch sử mua hàng</Link>) và lý do yêu cầu.</p>
        </Section>
        <Section title="4. Xử lý và hoàn tiền">
          <p>Yêu cầu được phản hồi trong vòng {site.refundProcessingDays} ngày làm việc. Khi được chấp nhận, quyền truy cập khóa học bị thu hồi, đơn hàng chuyển sang trạng thái "Đã hoàn tiền" và tiền được hoàn về phương thức thanh toán ban đầu (VNPay hoặc MoMo). Thời gian tiền về tài khoản phụ thuộc ngân hàng hoặc ví.</p>
        </Section>
      </>
    ),
  },
  'bao-mat': {
    title: 'Chính sách bảo mật',
    icon: ShieldCheck,
    summary: 'Chúng tôi thu thập dữ liệu gì, dùng để làm gì và cách bảo vệ.',
    body: (
      <>
        <Section title="1. Dữ liệu chúng tôi thu thập">
          <ul className="list-disc space-y-1 pl-6">
            <li>Thông tin tài khoản: họ tên, email, ảnh đại diện (nếu bạn tải lên), số điện thoại (nếu bạn cung cấp).</li>
            <li>Dữ liệu học tập: khóa học đã mua, tiến độ học, kết quả bài kiểm tra, chứng chỉ, đánh giá.</li>
            <li>Dữ liệu giao dịch: mã đơn hàng, số tiền, trạng thái và mã giao dịch do cổng thanh toán trả về.</li>
          </ul>
          <p>Mật khẩu được lưu dưới dạng băm một chiều. <b className="text-ink">Chúng tôi không lưu thông tin thẻ hay tài khoản ngân hàng</b>; việc thanh toán diễn ra trên trang của VNPay hoặc MoMo.</p>
        </Section>
        <Section title="2. Mục đích sử dụng">
          <ul className="list-disc space-y-1 pl-6">
            <li>Cung cấp khóa học, lưu tiến độ và cấp chứng chỉ.</li>
            <li>Xử lý thanh toán, hóa đơn và yêu cầu hỗ trợ, hoàn tiền.</li>
            <li>Gửi email cần thiết cho tài khoản (xác nhận email, đặt lại mật khẩu) và thông báo trong tài khoản (đơn hàng, chứng chỉ).</li>
          </ul>
        </Section>
        <Section title="3. Thông tin công khai trên chứng chỉ">
          <p>Để nhà tuyển dụng xác minh được, trang xác minh chứng chỉ hiển thị công khai: họ tên trên chứng chỉ, tên khóa học, giảng viên, ngày cấp, điểm bài kiểm tra và tình trạng hiệu lực. Người có mã chứng chỉ mới tra cứu được.</p>
        </Section>
        <Section title="4. Cookie">
          <p>Website dùng một cookie bảo mật (HttpOnly) để duy trì phiên đăng nhập. Chúng tôi không dùng cookie quảng cáo của bên thứ ba.</p>
        </Section>
        <Section title="5. Chia sẻ dữ liệu">
          <p>Chúng tôi không bán dữ liệu cá nhân. Dữ liệu chỉ được chia sẻ với: cổng thanh toán (để xử lý giao dịch), dịch vụ lưu trữ tệp và gửi email mà website sử dụng, giảng viên (họ tên, email và tiến độ của học viên trong khóa học của họ), hoặc cơ quan nhà nước khi có yêu cầu hợp pháp.</p>
        </Section>
        <Section title="6. Quyền của bạn">
          <p>Bạn có thể xem và sửa thông tin tại <Link to="/tai-khoan" className="font-semibold text-brand-700 hover:underline">Hồ sơ cá nhân</Link>. Để yêu cầu xóa tài khoản hoặc trích xuất dữ liệu, gửi email tới <a href={`mailto:${site.email}`} className="font-semibold text-brand-700 hover:underline">{site.email}</a>.</p>
        </Section>
      </>
    ),
  },
}

export function PolicyPage() {
  const { slug = '' } = useParams()
  const p = policies[slug]
  useSeo({ title: p?.title, description: p?.summary, noindex: !p })
  if (!p) return <NotFoundPage />
  return (
    <>
      <PageHeader eyebrow="Chính sách" title={p.title} crumbs={[{ label: 'Chính sách' }, { label: p.title }]}
        description={<>{p.summary} <span className="text-ink-muted">Cập nhật ngày {site.policyUpdatedAt}.</span></>} />
      <div className="container-page grid gap-8 py-12 lg:grid-cols-[260px_1fr] lg:items-start">
        <nav aria-label="Các chính sách" className="card flex flex-col gap-1 p-3 lg:sticky lg:top-24">
          {Object.entries(policies).map(([key, item]) => (
            <NavLink key={key} to={`/chinh-sach/${key}`}
              className={({ isActive }) => `flex min-h-11 items-center gap-3 rounded-lg px-3 font-medium ${isActive ? 'bg-brand-50 text-brand-700' : 'text-ink-soft hover:bg-surface-2 hover:text-ink'}`}>
              <item.icon className="size-4" aria-hidden="true" />{item.title}
            </NavLink>
          ))}
          <Link to="/lien-he" className="mt-2 flex min-h-11 items-center gap-3 rounded-lg border-t border-line/70 px-3 pt-2 font-medium text-ink-soft hover:text-ink">
            <Mail className="size-4" aria-hidden="true" />Liên hệ hỗ trợ
          </Link>
        </nav>
        <article className="card flex max-w-3xl flex-col gap-8 p-6 leading-relaxed text-ink-soft sm:p-10">{p.body}</article>
      </div>
    </>
  )
}
