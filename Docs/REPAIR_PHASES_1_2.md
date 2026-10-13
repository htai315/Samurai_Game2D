# Sửa lỗi đợt 1–2

Đây là ghi chép đợt 1–2. Đợt 3 đã bổ sung Play Again/hồi sinh và save v2;
xem [REPAIR_PHASE_3.md](REPAIR_PHASE_3.md) để biết hành vi hiện tại.

## Phạm vi

- 7 enemy Animator có đủ bool `isMoving`, trigger `Attack`, `Die`.
- Golem chuyển Idle/Walk theo `isMoving`, có thể đánh từ cả hai trạng thái.
  Các quái không có clip idle riêng tiếp tục dùng clip di chuyển hiện có.
- 179 coin trong 5 scene có ID riêng, lưu dưới dạng prefab override.
- Khi lưu scene trong Editor, ID coin bị thiếu, kế thừa từ prefab hoặc trùng
  sẽ được sửa. Khi build, validator báo lỗi nếu còn ID thiếu/trùng.
- HP, mana, lives không bị các hàm khởi tạo ghi đè sau Continue.
- SpawnLocator áp dụng save hoặc đặt spawn đúng một lần mỗi lần tải scene,
  rồi mới chụp snapshot đầu màn. Continue không đọc file lần thứ hai.
- Save được kiểm tra dữ liệu trước khi ghi; ghi qua file tạm rồi thay thế
  file chính và giữ bản trước ở `save.json.bak`.
- Save & Quit chỉ về menu khi lưu thành công. Lỗi được hiển thị trong game.

## Tương thích save

Save mới dùng `version: 1`. Khi Continue save cũ không có version:

1. Giữ bản gốc tại `save.json.legacy-v0.bak`, không ghi đè bản backup này.
2. Giữ vị trí, tiền, chỉ số và số mạng. Save rất cũ chưa có mạng được đặt 3 mạng.
3. Xóa danh sách coin đã nhặt vì ID cũ dùng chung, không xác định được coin nào
   đã nhặt. Vì vậy coin xuất hiện trở lại một lần sau chuyển đổi.
4. Lưu version mới; những lần Continue tiếp theo không reset danh sách coin.

Save hỏng, phiên bản tương lai, HP/mạng bằng 0 hoặc chỉ số không hợp lệ
sẽ bị từ chối. Game không tự khôi phục file backup hay tự xóa save lỗi.

## Kiểm tra tự động

`Assets/Editor/RepairValidation.cs` chạy kiểm tra save, asset và Play Mode.
Chỉ chạy trên **bản sao dự án**, không chạy trên phiên Editor đang làm việc:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.49f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'DUONG_DAN_BAN_SAO' -executeMethod RepairValidation.Run -logFile 'DUONG_DAN_LOG'
```

Không thêm `-quit`: runner tự thoát sau Play Mode và trả mã 0 nếu đạt.
Runner thay company/product name trong bản sao để dùng thư mục save riêng.
Kiểm tra gồm round-trip, backup, lỗi ghi, JSON hỏng, version không hỗ trợ,
migration một lần, ID coin, Animator, Continue sau Start, snapshot,
chuyển màn, Save & Quit và New Game.

Kết quả chạy ngày 12/10/2026 với Unity 6000.0.49f1:

- Build C# runtime và Editor: 0 lỗi; còn cảnh báo cũ CS0414
  (`PlayerCombat.attackCooldown` chưa dùng).
- Save checks: đạt.
- Asset checks: đạt, 7 controller và 179 coin.
- Play Mode checks: đạt (gồm thao tác qua các hàm nút menu thực tế).
- Log lần chạy thành công: `Logs/repair-phases-1-2.log` (không đưa vào Git).
- Chưa đánh giá trực quan animation/âm thanh hay build bản phát hành standalone.

Bản sao kiểm thử còn tại
`Temp/RepairValidation-f2cf132476ad4e48bfd00ca4b2ae0a94`.
Phiên Unity kiểm thử đã thoát; log đã được giữ riêng. Lệnh dọn thư mục tạm
bị môi trường chặn, nên thư mục này chưa được xóa. Có thể xóa riêng bản sao
này bằng tay khi không cần kiểm tra lại; không xóa thư mục dự án gốc.

## Kiểm tra thủ công trong Editor

Nếu scene đang mở báo đã thay đổi ngoài Editor, giữ lại các chỉnh sửa chưa lưu
của bạn trước khi reload; tránh ghi đè bản scene trên đĩa một cách vô ý.

1. New Game: HP đầy, mana theo prefab, 3 mạng, 0 coin.
2. Nhặt một coin: coin cạnh đó vẫn tồn tại, tiền chỉ tăng một lần.
3. Mất HP, tiêu mana, nâng cấp, mất một mạng; đứng xa spawn rồi Save & Quit.
4. Continue: đúng vị trí, HP/MP, chỉ số, tiền, mạng; chỉ coin đã nhặt biến mất.
5. Qua màn: xuất hiện tại spawn đích, giữ các chỉ số đang có.
6. Thử từng quái và Golem: không còn cảnh báo thiếu `isMoving`;
   kiểm tra trực quan đi/đứng/đánh/chết và thời điểm gây sát thương.

Chưa thay đổi thiết kế Play Again, giới hạn dùng skill hay tái cấu trúc toàn bộ
chuyển scene (các đợt sau của kế hoạch).
