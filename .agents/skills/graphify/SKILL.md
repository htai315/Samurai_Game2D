---
name: graphify
description: Tra cứu cấu trúc mã gameplay Samurai_Game2D, tìm lớp C#, quan hệ phụ thuộc và đường liên kết bằng Graphify.
---

Chạy từ thư mục gốc dự án với Python có cài Graphify. Trên máy phát triển hiện tại, Python dùng bản tại D:\graphify\graphify.

- Xem một lớp bằng `python -m graphify explain "BossPhaseState"`.
- Tìm đường liên kết bằng `python -m graphify path "BossHealth" "BossPhaseState"`. Nếu tên mơ hồ, chọn ID từ danh sách ứng viên.
- Mở `graphify-out/graph.html` để duyệt trực quan.

Nếu thiếu chỉ mục hoặc mã gameplay đã thay đổi, dựng lại bằng PowerShell:

```powershell
$env:GRAPHIFY_OUT = Join-Path $PWD 'graphify-out'
$env:PYTHONUTF8 = '1'
python -m graphify update Assets/Scripts
```

Chỉ mục đọc mã trong Assets/Scripts. Đối chiếu các quan hệ suy ra với mã nguồn. Với liên kết qua Inspector, Animation Event hoặc scene override, kiểm tra thêm prefab, clip và scene tương ứng.
