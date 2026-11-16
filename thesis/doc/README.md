# doc: Báo cáo đồ án dạng Word

Đồ án học phần **Chuyên đề ASP.NET**, đề tài **Xây dựng website chia sẻ khóa học trực tuyến**. Báo cáo trình bày theo mẫu `220064_MauBaoCao` và quy định hình thức của khoa: Times New Roman 13pt, giãn dòng 1,5, cách đoạn 6pt, lề trên 2 cm, dưới 2 cm, trái 3 cm, phải 2 cm, số trang ở góc phải dưới.

| Tệp | Nội dung |
| --- | --- |
| `Bia_DoAn_PhamHoangDuy_170125375.docx` / `.doc` | Bìa chính (theo mẫu `220064_MauBia`), dùng lại làm bìa phụ |
| `BaoCao_DoAn_PhamHoangDuy_170125375.docx` / `.doc` | Nhận xét GVHD, nhận xét TVHĐ, lời cảm ơn, mục lục, danh mục hình, bảng, chữ viết tắt, tóm tắt, Mở đầu, Chương 1–4, tài liệu tham khảo (IEEE), phụ lục |
| `hinh/` | Hình trong báo cáo: `so-do-*.png` (sơ đồ bài toán, ERD, use case, tuần tự, trạng thái, kiến trúc, triển khai) và `anh-*.png` (ảnh chụp chức năng có địa chỉ trang và thời điểm chụp) |

Bản PDF gộp bìa chính, bìa phụ và báo cáo: [`../pdf/BaoCao_DoAn_PhamHoangDuy_170125375.pdf`](../pdf/BaoCao_DoAn_PhamHoangDuy_170125375.pdf).

## Bố cục (4 chương, bỏ chương Tổng quan theo ghi chú của mẫu)

| Phần | Nội dung chính |
| --- | --- |
| Mở đầu | Lý do chọn đề tài, mục tiêu, đối tượng và phạm vi, phương pháp nghiên cứu |
| Chương 1. Nghiên cứu lý thuyết | ASP.NET Core, REST, EF Core, Clean Architecture, Identity và JWT, bảo mật, cổng thanh toán, React, công cụ kiểm thử và triển khai |
| Chương 2. Hiện thực hóa nghiên cứu | Mô tả bài toán, yêu cầu chức năng, quy tắc nghiệp vụ, mô hình CSDL (ERD, mô tả bảng), use case, thiết kế xử lý, kiến trúc, cài đặt |
| Chương 3. Kết quả nghiên cứu | Giao diện chức năng của khách, học viên, giảng viên, quản trị viên; Swagger; điện thoại; kết quả kiểm thử |
| Chương 4. Kết luận và hướng phát triển | Kết quả đạt được, hạn chế, hướng phát triển |

## Việc cần làm trước khi nộp

- Điền **học vị và họ tên giảng viên hướng dẫn** ở bìa (`[ThS./TS.] [Họ tên]`) và ở đầu trang của báo cáo (`[ThS./TS.] [Họ tên GVHD]`). Trong Word: Ctrl+H thay chuỗi trên.
- Sau khi sửa, nhấn Ctrl+A rồi F9 để cập nhật mục lục và danh mục hình, bảng.

## Chụp lại ảnh chức năng

Ảnh `anh-*.png` được chụp tự động bằng Playwright từ môi trường kiểm thử (API cổng 5081, web cổng 5174, dữ liệu mẫu):

```powershell
cd src\frontend\online-learning-client
$env:E2E_CHANNEL="chrome"; npx playwright test -c playwright.docs.config.ts
```
