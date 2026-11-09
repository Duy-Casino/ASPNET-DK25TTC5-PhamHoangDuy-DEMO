# 05 — Design system

Nguồn token: [`src/frontend/online-learning-client/src/index.css`](../../src/frontend/online-learning-client/src/index.css) (Tailwind v4 `@theme`).
Trang xem trước: chạy frontend và mở http://localhost:5173.

## Màu

Lấy cảm hứng từ cổng tuyển sinh Đại học Văn Hiến: xanh dương rực, nền trắng/xanh rất nhạt, điểm nhấn cam và vàng nắng.

| Vai trò | Token | Hex | Dùng cho |
| --- | --- | --- | --- |
| Chính | `brand-600` | #005CF8 | Nút chính, liên kết, khối hero |
| Chính đậm | `brand-700` | #004DC1 | Hover, mặt phẳng trên nền xanh |
| Nền nhạt | `brand-50` | #EFF6FF | Nền khu vực, nút phụ |
| Nhấn | `accent-500` | #EF8000 | Hành động mua, giá khuyến mãi — **chữ màu ink**, không dùng chữ trắng (tương phản 2.7:1) |
| Vàng | `sun-300` | #FFDC7C | Nhãn "Bán chạy", highlight |
| Chữ | `ink` | #0B1B33 | Văn bản chính |
| Chữ phụ | `ink-muted` | #6B7686 | Ghi chú, metadata |
| Mặt phẳng | `surface`, `surface-2`, `surface-3` | #FFF, #F3F6FB, #E8EEF7 | Tách vùng bằng bậc tông |
| Trạng thái | `success`, `warning`, `danger` (+ `-50`) | | Toast, badge trạng thái đơn hàng/khóa học |

Tương phản đã kiểm: trắng trên `brand-600` = 5.4:1 (AA), `ink` trên `accent-500` ≈ 7:1.

## Chữ

- Một họ chữ: **Be Vietnam Pro** (400–800), thiết kế cho tiếng Việt, dấu không bị chồng.
- Cỡ: `text-display` (40→88px, -0.035em, leading 1), `text-title` (28→44px), thân bài 16–18px, leading 1.6.
- Căn trái mặc định; tiêu đề tối đa 2 dòng.

## Thành phần

- Nút: phẳng, một màu, pill (`--radius-control`), cao ≥ 44px, nhấn co `scale(0.97)` trong 140ms `ease-out-strong`.
  Biến thể: `btn-primary`, `btn-accent`, `btn-soft`, `btn-ghost`.
- Thẻ: bo 12px (`--radius-card`), tách nền bằng `surface-2`; không shadow khi tĩnh, chỉ menu/dialog nổi mới có shadow.
- Không gradient, không glassmorphism, không icon trong ô màu lặp lại, không nhãn chữ hoa nhỏ giãn cách.

## Chuyển động (theo nguyên tắc của Emil Kowalski)

- Chỉ animate `transform` và `opacity`; dùng `ease-out` cho phần tử xuất hiện, không dùng `ease-in`.
- UI tương tác ≤ 250ms; không animate thao tác bàn phím lặp lại.
- Tôn trọng `prefers-reduced-motion` (đã có trong base layer).

## Áp dụng cho các màn hình

- **Trang công khai** (trang chủ, chi tiết khóa học): theo quy trình taste-skill — hero chiếm trọn màn đầu với màu `brand-600`, ảnh thật của khóa học, mỗi section một bố cục khác nhau.
- **Dashboard** (học viên/giảng viên/admin): nền `surface-2`, panel trắng, biểu đồ Recharts dùng `brand-600` cho chuỗi chính và `accent-500` cho chuỗi so sánh.
