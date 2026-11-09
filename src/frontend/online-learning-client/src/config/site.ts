/**
 * Thông tin doanh nghiệp hiển thị trên website (footer, liên hệ, chính sách, SEO).
 * Sửa ở một chỗ này khi có thông tin chính thức.
 *
 * Điều khoản hoàn tiền (refund*) đã được chủ website xác nhận ngày 06/11/2026.
 * Khi đổi, cập nhật luôn `policyUpdatedAt`. Nên nhờ người có chuyên môn pháp lý rà soát
 * các trang /chinh-sach/* trước khi nhận thanh toán thật.
 *
 * CẦN THAY: email, điện thoại, địa chỉ hiện là thông tin mẫu — thay bằng thông tin thật trước khi triển khai.
 */
export const site = {
  name: 'EduViet',
  tagline: 'Khóa học trực tuyến có chứng chỉ xác minh',
  description: 'EduViet — học trực tuyến cùng giảng viên Việt, có chứng chỉ xác minh.',
  /** CẦN THAY: thông tin liên hệ mẫu */
  email: 'hotro@eduviet.local',
  phone: '1900 1234',
  phoneHref: 'tel:19001234',
  address: 'TP. Hồ Chí Minh',
  supportHours: 'Thứ Hai – Thứ Sáu, 8:00 – 17:30',
  /** Mạng xã hội: chỉ hiện khi có địa chỉ thật */
  social: [] as { label: string; href: string }[],
  /** Số ngày được yêu cầu hoàn tiền kể từ ngày thanh toán (đã xác nhận) */
  refundDays: 7,
  /** Tỷ lệ nội dung tối đa đã học để còn được hoàn tiền (đã xác nhận) */
  refundMaxProgressPercent: 20,
  /** Số ngày làm việc để phản hồi yêu cầu hoàn tiền (đã xác nhận) */
  refundProcessingDays: 7,
  /** Ngày cập nhật chính sách gần nhất (hiển thị trên trang chính sách) */
  policyUpdatedAt: '06/11/2026',
  /** Ngưỡng tối thiểu để hiển thị số liệu trên trang chủ (tránh khoe con số quá nhỏ) */
  statThresholds: { students: 100, courses: 20, instructors: 10, certificates: 50 },
} as const
