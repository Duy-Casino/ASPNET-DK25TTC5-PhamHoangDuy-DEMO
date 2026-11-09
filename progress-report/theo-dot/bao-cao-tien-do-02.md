# Báo cáo tiến độ số 02

| | |
| --- | --- |
| Thời gian | 06/11/2026 |
| Sinh viên | Phạm Hoàng Duy, MSSV 170125375, lớp DK25TTC5 |
| Commit | `f1da8d0`, `eb08703` |

## 1. Mục tiêu

Làm mới giao diện, bổ sung các trang và chức năng giúp người mua tin tưởng, tối ưu SEO và hiệu năng.

## 2. Công việc đã thực hiện

**Thiết kế lại giao diện** (phong cách nền tảng công nghệ, giữ thương hiệu EduViet)
- Design system mới: font tiêu đề Lexend, màu nhấn, thẻ bo góc, trạng thái nút hover/active/disabled.
- Component dùng chung: PageHeader, SectionHeading, CourseCard.
- Header có menu biểu tượng, dropdown lĩnh vực, ô tìm kiếm; footer có dải kêu gọi hành động.
- Trang chủ mới: hero, lợi ích, tab lĩnh vực, hành trình học, đánh giá, giảng viên, xác minh chứng chỉ. Đồng bộ toàn bộ trang con.

**Người mua**
- Trang Giới thiệu, Liên hệ, Điều khoản, Chính sách hoàn tiền, Chính sách bảo mật.
- Điều khoản hoàn tiền đã chốt: yêu cầu trong 7 ngày, đã học tối đa 20%, phản hồi trong 7 ngày làm việc.
- Chuông thông báo trên header.

**SEO, hiệu năng**
- Title, description, canonical, Open Graph; JSON-LD Course và WebSite.
- `robots.txt`, `sitemap.xml` sinh từ API.
- Ảnh responsive (`srcset`), tách gói JavaScript.

## 3. Kết quả

- Backend **31/31**, E2E **19/19** đạt.
- Tài liệu design system: `thesis/html/13-design-system.md`.

## 4. Khó khăn và cách xử lý

| Khó khăn | Cách xử lý |
| --- | --- |
| Tham khảo phong cách trang khác nhưng không được sao chép nhận diện thương hiệu | Chỉ học bố cục và ngôn ngữ thiết kế; giữ logo, màu, nội dung, dữ liệu thật của EduViet |
| Kịch bản E2E hỏng do bố cục mới (bộ chọn trùng phần tử) | Sửa bộ chọn theo vai trò và vùng trang |
| Thông tin liên hệ doanh nghiệp chưa có | Tập trung vào `src/config/site.ts`, đánh dấu "CẦN THAY" để thay trước khi triển khai |

## 5. Kế hoạch tiếp theo

- Tăng uy tín giảng viên: hồ sơ giảng dạy, ảnh đại diện, trao đổi với học viên.
- Xây dựng tính năng điểm thưởng.
