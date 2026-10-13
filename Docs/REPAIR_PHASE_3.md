# Đợt 3: Play Again và hồi sinh

Ghi chép đợt 3; luật dùng skill đã được bổ sung ở
[REPAIR_PHASE_4.md](REPAIR_PHASE_4.md).

## Quy tắc

- Còn mạng: hồi sinh tại checkpoint của màn, HP đầy; giữ tiền, nâng cấp,
  mana và coin đã nhặt. Không chụp lại mốc đầu màn.
- Play Again: tải lại màn để khôi phục quái/đồ nhặt, về vị trí đầu màn,
  khôi phục HP/MP, giới hạn HP/MP, damage, tốc độ, bonus vĩnh viễn,
  tiền và danh sách coin theo mốc đầu màn. Cấp lại đủ số mạng tối đa.
- Coin đã nhặt trước mốc đầu màn vẫn được ghi nhận; coin nhặt trong lượt
  vừa thua xuất hiện lại. Bấm Play Again liên tiếp không tải màn nhiều lần.
- Hồi sinh xóa cờ chết, bị thương, đánh, dash và thi triển bị ngắt; reset
  vận tốc, Rigidbody, Animator, lượt nhảy và input di chuyển đang giữ.
- Hồi sinh checkpoint giữ cooldown đang chạy. Play Again/Continue khởi tạo
  lại cooldown và bỏ buff tạm thời; bonus vĩnh viễn lấy từ snapshot.
- Quit ở Game Over dọn PersistentRoot theo component, kể cả root tên
  `PersistentRoot(Clone)`, và xóa các singleton tương ứng.

## Save và tương thích

Save version 2 chứa cả trạng thái hiện tại và bản sao độc lập của mốc đầu màn.
Save & Continue giữa màn không thay mốc đầu màn bằng điểm vừa lưu.

- Save v1: giữ nguyên coin và tiến trình khi chuyển sang v2.
- Save v0: vẫn áp dụng quy tắc backup/reset danh sách coin một lần của đợt 1–2.
- Save chưa chứa mốc đầu màn: dùng trạng thái Continue làm mốc dự phòng.
  Không thể suy ra các chỉ số thật tại đầu màn từ những save này.
- Play Again chỉ đổi trạng thái đang chơi, không tự ghi đè file save trên đĩa.
  Dùng Save & Quit hoặc F5 để lưu lượt chơi mới.
- Mốc sai scene hoặc chỉ số không hợp lệ bị từ chối trước khi áp dụng.

## Kiểm tra

Runner: `RepairValidation.Run`, sau bộ kiểm tra đợt 1–2 sẽ gọi
`Phase3Validation`. Chỉ chạy batch mode trên bản sao dự án; runner đổi
company/product name ở bản sao để không dùng save thật.

Các ca kiểm tra:

1. Save v0/v1/v2, snapshot độc lập, snapshot sai scene.
2. Cả 5 màn có một LevelRespawnManager và checkpoint đã gán.
3. Nhặt coin, nâng cả 4 chỉ số, nhận bonus, rồi chết khi còn mạng.
4. Hồi sinh đúng checkpoint, giữ tiến trình, hết các cờ hành động bị kẹt.
5. Save & Continue giữa màn giữ mốc đầu màn cũ.
6. Hết mạng, Play Again, bấm lặp: hoàn tác đủ chỉ số/coin, khôi phục quái,
   đủ mạng, một PersistentRoot, điều khiển được mở lại.
7. Save & Continue sau Play Again và Quit về menu.

Log: `Logs/repair-phase-3-final.log` (không đưa vào Git).
Lần chạy ngày 12/10/2026 với Unity 6000.0.49f1: toàn bộ kiểm tra đợt 1–2
và đợt 3 đều đạt, Unity trả mã thoát 0. Build C# runtime/Editor không lỗi;
còn cảnh báo cũ CS0414 về `PlayerCombat.attackCooldown` chưa dùng.
Kiểm tra headless không thay thế việc nhìn animation/fade và chơi thử bằng bàn phím.

Bản sao kiểm thử nằm trong `.utmp/Phase3Validation-a48f5f4e209f4cc2ae0b841f76051956`,
không đưa vào Git. Phiên Unity kiểm thử đã thoát; giữ lại bản sao để tái hiện kiểm tra.

Chưa thay đổi luật sử dụng skill khi chết/tạm dừng/chuyển màn; đợt này chỉ
thêm reset các cờ thi triển khi hồi sinh. Chưa làm đợt tái cấu trúc chuyển màn.
