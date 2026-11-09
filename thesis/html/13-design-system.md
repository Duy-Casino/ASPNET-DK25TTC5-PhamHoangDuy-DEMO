# 13 — Design system giao diện

Phong cách: "technology platform" sáng, thoáng (tham khảo ngôn ngữ thiết kế của các trang giới thiệu nền tảng công nghệ như digibird.co). Thương hiệu EduViet được giữ nguyên: logo, màu xanh #005CF8, cam #EF8000, nội dung và dữ liệu thật từ API.

Mã nguồn: token và utility nằm trong `src/frontend/online-learning-client/src/index.css`, component dùng chung nằm trong `src/components/ui/index.tsx`.

## Token

| Nhóm | Giá trị |
| --- | --- |
| Màu chính | `brand-50 … brand-900`, chính là `brand-600` #005CF8 |
| Màu nhấn | `accent-*` (cam, dùng cho giá và đánh giá); `mint-*` (trang trí: gạch chân từ khóa, khối nền, gạch dưới menu đang chọn) |
| Nền | `surface` #fff, `surface-2` #f6f8fc (dải tiêu đề, section xen kẽ), `line` #e4e9f1 |
| Chữ | Tiêu đề **Lexend** (h1–h3, `.font-display`), thân bài **Be Vietnam Pro**. Cả hai hỗ trợ tiếng Việt |
| Cỡ chữ | `text-display` (hero, 36–58px) · `text-title` (h1 trang con, h2 section, 27–38px) · `text-heading` (h3 thẻ, 20px) |
| Bo góc | `--radius-card` 16px · `--radius-control` 10px (nút, ô nhập) |
| Đổ bóng | `--shadow-soft` (thẻ khi tĩnh) · `--shadow-lift` (hover, vùng nổi) · `--shadow-brand` (hover nút chính) |
| Khoảng cách | Thang 4px của Tailwind; section dọc 80–96px (`py-20 md:py-24`), khoảng cách giữa các thẻ 24px |
| Breakpoint | sm 640 · md 768 (tablet) · lg 1024 (menu đầy đủ) · xl 1280 |

## Utility

| Utility | Dùng cho |
| --- | --- |
| `btn-primary` / `btn-outline` | Cặp CTA chính / phụ. Hover nhấc 1px và có bóng, active co 0.97, disabled mờ 50% |
| `btn-soft` / `btn-ghost` / `btn-accent` / `btn-light` | Hành động thứ cấp, nút trong thanh công cụ, nút cam, nút trên nền màu |
| `card` + `card-hover` | Thẻ trắng có viền mảnh và bóng nhẹ, thẻ bấm được nhấc lên khi hover |
| `input` | Ô nhập: hover và focus có viền xanh và vòng sáng; có trạng thái `aria-invalid` và `disabled` |
| `chip`, `eyebrow`, `mark-mint` | Nhãn viên thuốc, nhãn nhỏ trên tiêu đề, gạch chân từ khóa |
| `float-slow`, `rise-in`, `[data-reveal]` + `useReveal` | Chuyển động nhẹ. Tất cả tắt khi người dùng bật "giảm chuyển động" |

## Component dùng chung

- `PageHeader`: dải tiêu đề trang con gồm đường dẫn, nhãn, h1, mô tả và hành động. Đang dùng ở các trang Khóa học, Giỏ hàng, Thanh toán, Lịch sử mua hàng.
- `SectionHeading`: tiêu đề section, căn trái kèm hành động hoặc căn giữa.
- `DecorBlobs`: khối tròn mờ trang trí nằm phía sau nội dung.
- `ResponsiveImg`: ảnh có `srcset`/`sizes` (Cloudinary dùng `f_auto,q_auto`; picsum đổi kích thước theo URL), mặc định tải lười.
- `CourseCard` / `CourseGrid`: thẻ khóa học dạng card, hiển thị lĩnh vực, thông số bài, thời lượng, học viên và giá.
- Header (`MainLayout`): menu có biểu tượng, menu thả xuống "Khóa học" lấy lĩnh vực từ API, ô tìm kiếm mở bằng biểu tượng kính lúp, có bóng khi cuộn, menu hamburger trên điện thoại. Footer: dải CTA và 4 cột liên kết.
