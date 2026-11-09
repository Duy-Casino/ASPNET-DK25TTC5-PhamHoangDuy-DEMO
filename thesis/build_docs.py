"""Gộp tài liệu kỹ thuật thesis/html/NN-*.md thành một trang HTML và một tệp PDF.

Cách dùng (tại thư mục gốc repository):
    pip install markdown
    python thesis/build_docs.py

Kết quả:
    thesis/html/tai-lieu-ky-thuat.html
    thesis/pdf/tai-lieu-ky-thuat.pdf   (cần Microsoft Edge hoặc Google Chrome để in PDF)

Chạy lại script mỗi khi sửa các tệp .md để bản HTML/PDF luôn khớp.
"""
import datetime
import html
import pathlib
import re
import shutil
import subprocess

import markdown

ROOT = pathlib.Path(__file__).resolve().parent
SRC = ROOT / 'html'
OUT_HTML = SRC / 'tai-lieu-ky-thuat.html'
OUT_PDF = ROOT / 'pdf' / 'tai-lieu-ky-thuat.pdf'

CSS = """
body{font-family:"Segoe UI","Be Vietnam Pro",Arial,sans-serif;max-width:960px;margin:0 auto;padding:24px 16px;color:#1b2333;line-height:1.6;background:#fff}
h1{color:#005CF8;border-bottom:2px solid #e4e9f1;padding-bottom:6px;margin-top:48px}
h2{margin-top:32px}h1,h2,h3{line-height:1.3}
table{border-collapse:collapse;width:100%;margin:12px 0;font-size:14px}
th,td{border:1px solid #d5dce8;padding:6px 8px;text-align:left;vertical-align:top}
th{background:#f6f8fc}
code{background:#f1f4f9;padding:1px 4px;border-radius:4px;font-size:90%}
pre{background:#f6f8fc;border:1px solid #e4e9f1;padding:12px;overflow-x:auto;border-radius:6px}
pre code{background:none;padding:0}
blockquote{border-left:4px solid #EF8000;margin:12px 0;padding:4px 12px;background:#fff8ef}
.cover{text-align:center;padding:80px 0 40px}
.cover h1{border:none;font-size:34px;margin:0}
.toc a{text-decoration:none}
section.doc{page-break-before:always}
@media print{body{max-width:none;padding:0}a{color:inherit}}
"""


def anchor(name: str) -> str:
    return 'doc-' + name.removesuffix('.md')


def main() -> None:
    files = sorted(p for p in SRC.glob('[0-9][0-9]-*.md'))
    md = markdown.Markdown(extensions=['tables', 'fenced_code', 'toc', 'sane_lists'])
    toc, sections = [], []
    for f in files:
        text = f.read_text(encoding='utf-8')
        # Liên kết giữa các tài liệu -> neo trong cùng trang
        text = re.sub(r'\]\((\d\d-[^)#]+\.md)(#[^)]*)?\)', lambda m: f'](#{anchor(m.group(1))})', text)
        body = md.reset().convert(text)
        title = next((l[2:].strip() for l in text.splitlines() if l.startswith('# ')), f.stem)
        toc.append(f'<li><a href="#{anchor(f.name)}">{html.escape(title)}</a></li>')
        sections.append(f'<section class="doc" id="{anchor(f.name)}">{body}</section>')

    today = datetime.date.today().strftime('%d/%m/%Y')
    page = f"""<!doctype html>
<html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Tài liệu kỹ thuật EduViet</title><style>{CSS}</style></head><body>
<div class="cover">
<h1>EduViet: Website bán khóa học trực tuyến</h1>
<p><strong>Tài liệu kỹ thuật</strong></p>
<p>Sinh viên thực hiện: Phạm Hoàng Duy · MSSV 170125375 · Lớp DK25TTC5</p>
<p>Tạo ngày {today} từ thesis/html/*.md</p>
</div>
<h2>Mục lục</h2><ol class="toc">{''.join(toc)}</ol>
{''.join(sections)}
</body></html>"""
    OUT_HTML.write_text(page, encoding='utf-8')
    print('Đã tạo', OUT_HTML.relative_to(ROOT.parent))

    browser = next((b for b in (
        shutil.which('msedge'), shutil.which('chrome'),
        r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
        r'C:\Program Files\Google\Chrome\Application\chrome.exe',
    ) if b and pathlib.Path(b).exists()), None)
    if not browser:
        print('Không tìm thấy Edge/Chrome, bỏ qua bước tạo PDF.')
        return
    OUT_PDF.parent.mkdir(exist_ok=True)
    subprocess.run([browser, '--headless', '--disable-gpu', '--no-pdf-header-footer',
                    f'--print-to-pdf={OUT_PDF}', OUT_HTML.as_uri()], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
    print('Đã tạo', OUT_PDF.relative_to(ROOT.parent))


if __name__ == '__main__':
    main()
