# Báo cáo tiến độ số 04

| | |
| --- | --- |
| Thời gian | 09/11/2026 |
| Sinh viên | Phạm Hoàng Duy, MSSV 170125375, lớp DK25TTC5 |
| Commit | `50e9a94`, `1bc9bcb` và commit tổ chức lại repository |

## 1. Mục tiêu

Tổ chức repository theo quy định đồ án để thầy cô, bạn bè hiểu và chạy lại được chương trình; hoàn thiện README.

## 2. Công việc đã thực hiện

- Mời cộng tác viên trên GitHub để theo dõi tiến độ.
- Viết lại README: thông tin sinh viên, chức năng, kiến trúc, hướng dẫn cài đặt, tài khoản mẫu, kiểm thử, nhật ký tiến độ.
- Tổ chức lại cây thư mục:

| Thư mục | Nội dung |
| --- | --- |
| `setup/` | Script `run-demo.ps1`, script SQL (`database/`), mô tả dữ liệu thử (`test-data/`), sơ đồ triển khai |
| `src/` | Mã nguồn backend (ASP.NET Core) và frontend (React) cùng dữ liệu, kịch bản kiểm thử |
| `progress-report/` | Các báo cáo tiến độ |
| `thesis/` | `doc/`, `pdf/`, `html/`, `abs/`, `refs/`; tài liệu kỹ thuật có bản HTML và PDF sinh tự động |
| `soft/` | Danh sách phần mềm và liên kết tải |
| `docker/` | docker-compose và mẫu biến môi trường |

- Cập nhật mọi đường dẫn bị ảnh hưởng: CI GitHub Actions, docker-compose, tài liệu, chú thích trong mã.

## 3. Kết quả

- Sau khi chuyển thư mục: backend **43/43**, E2E **25/25** đạt; `npm run build` thành công.
- `thesis/pdf/tai-lieu-ky-thuat.pdf` và `thesis/html/tai-lieu-ky-thuat.html` gộp 14 tài liệu kỹ thuật.

## 4. Khó khăn và cách xử lý

| Khó khăn | Cách xử lý |
| --- | --- |
| Chuyển thư mục làm hỏng đường dẫn trong CI, Docker, tài liệu | Rà soát toàn bộ tham chiếu, sửa rồi chạy lại đủ test |
| Thư mục `bin/obj` cũ giữ đường dẫn tuyệt đối, test host bị crash | Xóa `bin/obj`, build lại từ đầu |
| Không lưu được bộ cài .NET/SQL Server vào repository (dung lượng, giấy phép) | Ghi liên kết tải chính thức trong `soft/`, script tự kiểm tra công cụ |

## 5. Kế hoạch tiếp theo

- Viết báo cáo đồ án Word (`thesis/doc/`) và slide, video demo (`thesis/abs/`).
- Thay thông tin liên hệ mẫu bằng thông tin thật trước khi triển khai.
- Thử triển khai với SQL Server thật và cổng VNPay/MoMo sandbox.
