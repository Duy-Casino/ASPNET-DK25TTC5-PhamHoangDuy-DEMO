# 07 — Frontend (Giai đoạn 3)

React 19 + TypeScript + Vite 8 + Tailwind CSS v4 + React Router 7 + Axios. Thông báo: Sonner. Lọc HTML: DOMPurify. Icon: lucide-react.

## 1. Chạy

```powershell
# Terminal 1 — API, chế độ demo không cần SQL Server (SQLite file eduviet-demo.db, tự seed dữ liệu mẫu)
cd khoa-hoc-online\backend\src\OnlineLearning.API
dotnet user-secrets set "Jwt:SecretKey" "<chuỗi ngẫu nhiên ≥ 32 ký tự>"
dotnet user-secrets set "Seed:DemoPassword" "<mật khẩu demo, có hoa/thường/số>"
dotnet run --launch-profile demo-sqlite

# Terminal 2 — Frontend
cd khoa-hoc-online\frontend\online-learning-client
npm install
npm run dev
```

Mở http://localhost:5173. Vite proxy chuyển `/api/*` tới `http://localhost:5080` (đổi bằng biến `API_PROXY_TARGET`).
Dùng SQL Server thật: chạy profile `https` thay cho `demo-sqlite` (cả hai đều mở http://localhost:5080).

## 2. Trang

| Đường dẫn | Trang | Ghi chú |
| --- | --- | --- |
| `/` | Trang chủ | Hero "chứng chỉ" (gõ tên để in lên chứng chỉ mẫu), lĩnh vực, nổi bật, bán chạy, phiếu thư viện, mới xuất bản, giảng viên, đánh giá, lời mời đăng ký |
| `/khoa-hoc` | Danh sách | Tìm kiếm, lọc lĩnh vực/cấp độ/giá/đánh giá, sắp xếp, phân trang; trạng thái lọc nằm trên URL |
| `/khoa-hoc/:slug` | Chi tiết | Thẻ mua hàng dính, xem thử video preview, đề cương, mô tả (DOMPurify), giảng viên, phân bố sao, đánh giá, viết/sửa đánh giá khi đã sở hữu |
| `/giang-vien/:id` | Hồ sơ giảng viên | |
| `/dang-nhap`, `/dang-ky`, `/quen-mat-khau`, `/dat-lai-mat-khau`, `/xac-nhan-email` | Tài khoản | Quay lại trang trước qua `?next=` (chỉ chấp nhận đường dẫn nội bộ) |
| `/gio-hang` | Giỏ hàng | Cần đăng nhập |
| `/thanh-toan[?course=id]` | Thanh toán | Báo giá từ server, mã giảm giá, đơn 0đ hoàn tất ngay |
| `/don-hang/:id` | Đơn hàng | Hóa đơn, đếm ngược hết hạn, hủy đơn |
| `/tai-khoan`, `/tai-khoan/don-hang` | Hồ sơ, đổi mật khẩu, lịch sử mua hàng | |

## 3. Xác thực phía client

- Access token chỉ giữ **trong bộ nhớ** (`tokenStore`), không dùng localStorage.
- Refresh token ở cookie HttpOnly. Khi tải trang, `AuthProvider` gọi `/api/auth/refresh` để khôi phục phiên; cứ 12 phút làm mới một lần.
- Interceptor Axios: gặp 401 → refresh một lần (gộp các request song song) → gửi lại.
- `ProtectedRoute` chặn trang cần đăng nhập/vai trò.

Lần tải đầu khi chưa đăng nhập, console có một dòng 401 từ `/api/auth/refresh`: đây là kiểm tra phiên, không phải lỗi.

## 4. Thiết kế

Hướng thiết kế lấy từ taste-skill (seed 1677846207), hướng **"Trang chính là vật thể"**:
*chứng chỉ có mã xác minh của EduViet trở thành tấm thẻ ở hero*. Các phần tiếp theo là giấy tờ cùng bộ: phiếu thư viện (mép răng cưa, cộng đúng tổng số khóa học), đánh giá in trên mẩu giấy xé, thẻ hồi đáp nghiêng, chân trang là mặt sau tấm thiệp với mép vỏ sò.

- Màu: xanh `#005CF8` / `#004DC1`, cam `#EF8000`, vàng `#FFDC7C`, nền `#EFF6FF` (tham khảo tuyensinh.vhu.edu.vn).
- Không gradient, không đổ bóng khi tĩnh (chỉ menu, hộp thoại và thẻ mua hàng nổi), không viền trừ ô nhập và bảng.
- Chuyển động: hero trồi lên, ảnh nền trôi chậm, các phần tử từng section xuất hiện lần lượt (stagger 70ms), phản hồi khi nhấn nút (`scale(0.97)`); tôn trọng `prefers-reduced-motion`.
- Kết quả đo `inspect.mjs` trên bản build production của trang chủ: **0 FAIL**, 2 WARN có lý do (số cỡ chữ của trang thương mại; video nằm ở trang chi tiết khóa học).

Ảnh khóa học và ảnh nền là ảnh Unsplash qua picsum.photos (chỉ để minh họa), sẽ thay bằng ảnh thật qua Cloudinary ở Giai đoạn 5.
