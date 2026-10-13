# Màn 6 — Moonlit Citadel

Scene: `Assets/Scenes/LV6-MoonlitCitadel.unity`.

## Cách vào

- Đi tới cổng kết thúc màn 5: nay chuyển sang màn 6, giữ player, chỉ số, coin và tiến trình nhặt đồ.
- Hoặc mở scene mới và nhấn Play để xem riêng: scene tự tạo PersistentRoot nếu chưa có.
- Hạ boss cuối màn 6 để mở cổng Ending. Cổng chỉ khóa tiến trình kết thúc, không nhốt player trong đấu trường.

## Thiết kế

643 đơn vị chiều ngang, 30 đoạn đường chính, 6 chương không gian:

1. The Silent Road — đường thành thoáng để làm quen.
2. Broken Ramparts — nhịp lên/xuống, ban công thưởng và điểm hồi sinh đầu.
3. The High Watch — đường leo lên cao nhất, có kiếm thưởng trên ban công.
4. Fallen Court — hạ thấp vào các sân chiến đấu.
5. The Last Ascent — leo trở lại thành, xen kẽ quái mặt đất và Bat.
6. Moonlit Citadel — điểm hồi sinh trước đấu trường, boss và cổng kết thúc.

Sau trùng tu v2: nền rừng hai lớp parallax, cây cổ thụ/rễ/đá từ màn 3, cỏ và hàng rào từ màn 2, hang đá từ màn 4, kết hợp cổng vòm/cột/cờ lâu đài. Sáu khu lần lượt là đường rừng, cầu thành cũ, vườn tháp canh, hang đá, thành bị rừng phủ và sân hộ vệ. Bỏ các tháp hình chữ nhật lặp lại; thêm móng có vân đá/rễ, trụ đỡ, đá vụn, đèn chỉ đường và trụ checkpoint. Tilemap móng dùng các ô đặc, không ghép nguyên mảng chứa nhiều ô mẫu trong atlas. Trang trí không có collider, nằm sau lớp quái/player để không che chiến đấu. Nhóm `06 - Renovation v2` chia riêng 30 đoạn để chỉnh tiếp trong Editor.

- 138 coin, 1 kiếm vĩnh viễn ở nhánh phụ, 5 bình máu.
- 28 quái tính cả boss; điểm đặt tính từ đáy collider, không đặt theo pivot tùy ý.
- 5 checkpoint. Kích hoạt lần đầu hồi đầy HP; không hồi lặp khi đứng trong trigger. Save/Continue khôi phục checkpoint đã kích hoạt; Play Again quay về snapshot đầu màn.
- Các khoảng trống chính rộng 2–3 đơn vị, bước cao tối đa 2. Thiết kế dùng khả năng nhảy đôi hiện tại (K), không bắt buộc dash hay nâng tốc độ. Sáu ban công thưởng nay cao 3,5 đơn vị, dày 0,5; khoảng trống bên dưới 3 đơn vị. Chúng không bắt buộc để qua màn.
- Boss riêng trong màn mới: 120 HP, damage 2, cooldown 2 giây, tốc độ 1,8, vùng đánh 2,2. Không thay prefab/chỉ số boss ở các màn cũ.

## Thay đổi hệ thống

- `EndItemGate`: nếu không hủy root, đây là cổng sang màn kế tiếp và không xóa SaveRuntime; cổng Ending thật vẫn hủy root/xóa phiên như cũ.
- Màn 5 đổi destination sang LV6, `destroyPersistentRoot=false`.
- `CitadelLevel`: bootstrap cho Play trực tiếp, khôi phục checkpoint và mở trigger Ending sau boss.
- `CitadelCheckpoint`: đánh dấu checkpoint bằng ID theo tên scene/index trong tập pickup đã có, tương thích snapshot hiện tại.
- `LevelRespawnManager.SetCheckpoint`: cập nhật điểm hồi sinh mà không thay cơ chế chết/trừ mạng.
- `CitadelBackdrop`: parallax ngang cho hai lớp rừng (0,12 và 0,045), không tác động collider.

## Tạo và kiểm thử

Scene được bake bằng `CitadelMapBuilder` trong bản sao dự án, sau đó đưa scene/material/tile/sprite metadata mới về dự án chính. Không cần generator chạy lúc chơi. Builder từ chối ghi đè scene đã tồn tại. Ảnh kiểm tra tại `Logs/citadel-preview-0.png` tới `-2.png`.

Trùng tu dùng `CitadelRenovation.Run` trên bản scene v1 trong `.utmp`; không chạy trên scene đang chỉnh ở dự án chính. Công cụ từ chối chạy lại khi đã có nhóm v2, tránh nhân đôi cảnh và nâng phần thưởng nhiều lần. Không dùng `CitadelMapBuilder.FinalizeAndPreview` để sửa v2 vì hàm cũ có đặt lại vị trí quái. Các scene 2/3/4 chỉ được mở để tham khảo, không lưu thay đổi. Sprite được cắt thành asset riêng, không thay importer/texture gốc.

Nguyên nhân bệ thấp v1: mặt bệ cao 3, dày 1 nên gầm chỉ cao 2, thấp hơn collider player khoảng 2,06. V2 sửa đồng bộ hình và collider, nâng coin/kiếm trên bệ thêm 0,5; giữ nguyên ID phần thưởng, checkpoint, 30 đoạn đường chính và vị trí quái.

Kiểm tra v2:

- `CitadelRenovation.ValidateClearance`: đo theo collider player thật, quét BoxCast qua cả 6 gầm theo hai hướng. Được gọi lại trong bộ `RepairValidation`.
- `CitadelTraversalValidation.Run`: Play Mode dùng Rigidbody2D thật, đi qua mỗi gầm từ hai phía rồi nhảy đơn từ cạnh bệ và đáp lên mặt bệ. Cả 6 vị trí đạt; log `Logs/citadel-traversal.log`. Fixture tắt AI để cô lập va chạm, đặt vận tốc tương đương đi/nhảy hiện tại, không phải kiểm thử bàn phím hay chiến đấu cùng lúc.
- Ảnh render tám vị trí: `Logs/citadel-v2-0.png`, `-3`, `-7`, `-12`, `-13`, `-17`, `-22`, `-29`; ảnh player dưới bệ kiếm: `Logs/citadel-v2-underpass.png`.
- Bộ hồi quy toàn dự án sau trùng tu: `Logs/citadel-v2-regression.log`, Unity thoát mã 0; 87 quái/boss, 1.548 tình huống hình học, 0 lỗi. Save/Continue, checkpoint, nhặt đồ, player/enemy và cổng LV5→LV6→Ending đều đạt. Build C#: 0 lỗi, 0 cảnh báo.
- Bản sao scene trước trùng tu: `.utmp/LV6-before-renovation-20261013.unity`. Không thay đổi các scene tham khảo 2/3/4 trong đợt trùng tu này.

`CitadelValidation` kiểm tra cổng thật LV5→LV6 giữ tiến trình, số đoạn đường và khoảng nhảy, checkpoint, Save/Continue, hồi sinh, Play Again và boss→Ending. Kiểm tra route là kiểm tra kích thước/khả năng thiết kế, chưa thay thế một lượt điều khiển player đi hết màn bằng input thật.

Bộ `EnemySceneGeometryValidation` tiếp tục quét cả scene mới; bộ hồi quy cũ vẫn chạy. Log lần chạy: `Logs/citadel-regression.log`.

Kết quả: Unity thoát mã 0, toàn bộ hồi quy và `CitadelValidation` đạt. Quét tổng cộng 87 quái/boss trong sáu màn, 1.548 tình huống hình học, 0 lỗi. Kiểm tra mở riêng scene/Play cũng đạt (`Logs/citadel-direct-play.log`). Build C#: 0 lỗi, 0 cảnh báo. Đã xem ba ảnh render để kiểm tra vật liệu, địa hình và bố cục.

## Cần chơi thử

Đây là màn mới có thể chơi thử, chưa phải bản cân bằng cuối. Cần đi trọn màn để chỉnh mật độ quái, cảm giác nhảy, vị trí camera khi lên cao và thời gian đánh boss với chỉ số player thực tế sau màn 5. Chưa đo Profiler/FPS trên máy đích. Không tự lưu đè save của người dùng trong quá trình kiểm thử.
