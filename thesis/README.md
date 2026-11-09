# thesis: Tài liệu văn bản của đồ án

| Thư mục | Nội dung | Trạng thái |
| --- | --- | --- |
| [`doc/`](doc/) | Báo cáo đồ án dạng Word (.docx) | Chờ bổ sung |
| [`pdf/`](pdf/) | Tài liệu dạng PDF. Hiện có `tai-lieu-ky-thuat.pdf`, gộp 14 tài liệu kỹ thuật | Đã có |
| [`html/`](html/README.md) | Tài liệu dạng web: 14 tệp Markdown (GitHub hiển thị như trang web) và bản gộp `tai-lieu-ky-thuat.html` | Đã có |
| [`abs/`](abs/) | Báo cáo trình bày trước Hội đồng: slide (.pptx), video demo | Chờ bổ sung |
| [`refs/`](refs/README.md) | Tài liệu tham khảo | Đã có danh mục |

## Cập nhật bản HTML/PDF

Nguồn chính là các tệp `html/NN-*.md`. Sau khi sửa, chạy tại thư mục gốc repository:

```powershell
pip install markdown
python thesis/build_docs.py
```

Script tạo lại `html/tai-lieu-ky-thuat.html` và `pdf/tai-lieu-ky-thuat.pdf`. Bước in PDF cần Microsoft Edge hoặc Google Chrome.
