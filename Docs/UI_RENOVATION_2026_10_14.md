# Trùng tu UI Samurai — 14/10/2026

## Phạm vi

Làm mới UI gameplay trên code hiện tại, không thay scene/map, animation combat,
save format, cân bằng hay các phím chiến đấu. Không yêu cầu gán lại Inspector.
Giữ GUID và các public callback của UI cũ để không làm mất liên kết prefab.

## Giao diện mới

- `SamuraiUI`: hệ tọa độ tham chiếu 1280×720 dùng chung, giữ tỉ lệ ở cả hai chiều;
  quản lý kiểu chữ/cache style, màu sơn mài–ngà–vàng trầm, nút và panel. Mỗi lượt
  vẽ khôi phục GUI matrix/color/depth/enabled, tránh chữ bị nhân với màu nền.
- Font Playfair Display được tải từ Resources đã có trong dự án, không ưu tiên
  font cài riêng trên Windows. Bỏ emoji/Kanji trang trí trong những vùng cần đọc nhanh.
- Guide: hai cột, phím riêng, tên thao tác lớn, mô tả ngắn và đúng với code.
  **F1 mở/đóng Guide; H/E giữ nguyên là hồi máu.** K nhảy, L lướt,
  C/Shift parry, U kiếm khí, I hỏa kiếm, Q/O ultimate.
- Stats: tên mới “Rèn luyện & hộ phù”, cột tên–giá trị–nút không giao nhau.
  Giá và bước nâng lấy từ `PlayerStats`, không ghi cứng 10 coin.
  Hiển thị số xu còn thiếu, chặn nút mua không đủ tiền, giữ phím nhanh 1/2/3.
- Shrine: cùng theme với Stats, mô tả hộ phù rõ, hỗ trợ mua trong pause do đúng
  giao diện sở hữu. Hành vi B/W và hiệu lực gameplay của hộ phù được giữ nguyên.
- `SamuraiCombatHUD`: gom L/U/I/Q vào một hàng ở dưới trái; hiển thị phím,
  tên và trạng thái sẵn sàng/hồi chiêu/thiếu mana/đang dùng/đang bận.
  Tái sử dụng sprite icon có sẵn; tắt riêng Graphic của các ô cooldown cũ ở runtime,
  không xóa prefab. Khôi phục các Graphic này khi HUD bị tắt/hủy.
- Combo: số lớn, rank ngắn, chỉ số nảy nhẹ; thưởng riêng, màu ngọc trầm.
  Không còn nhét Kanji + danh hiệu dài vào khung hẹp.
- Boss: cùng dải đáy, bên phải cụm kỹ năng, có vùng riêng để không chồng HUD
  hoặc nằm giữa thân nhân vật. Số HP trắng ngà đọc được; giữ vệt máu rút chậm.
- Banner màn: giữ tên/nội dung của từng chương, bỏ giãn chữ thủ công,
  dùng cùng font và phân cấp mới; ẩn khi đang mở bảng.
- Pause: cùng theme, nút Tiếp tục/Lưu và về menu, âm lượng kéo được;
  giữ nguyên API lưu và callbacks hiện có. UI Canvas cũ vẫn tồn tại để bảo toàn refs.

## Pause và quyền tương tác

`SamuraiModal` chỉ cho một bảng sở hữu pause tại một thời điểm. Hủy HitStop trước
khi lưu tốc độ cần khôi phục; đánh dấu frame đã nhận thao tác để một lần Esc
không vừa đóng bảng này vừa mở bảng khác. Chuyển scene hủy quyền modal.

Stats giờ thực sự pause gameplay. `GameManager` không tự đóng Stats vì pause của
chính nó. `PlayerStats` chỉ cho mua lúc pause nếu chủ sở hữu là Stats hoặc Shrine;
pause thường, chết, game over và chuyển scene vẫn chặn nâng cấp.

## Kiểm tra

Kết quả: build C# sạch (0 lỗi, 0 cảnh báo); UI runner qua 19 assertions và
render 7 ảnh; full RepairValidation qua toàn bộ; CombatUpgradeValidation qua
29 assertions. Đã trực tiếp xem ảnh Guide, Stats, Shrine, HUD và Pause, bao gồm
Stats ở 1024×768. Không sửa/giảm yêu cầu các bài test hồi quy để lấy kết quả pass.

Chạy trên bản sao cô lập, với company/product riêng để không chạm save thật:

```text
dotnet build Samurai_Game2D.sln --no-restore -v:minimal
Unity.exe -batchmode -projectPath COPY -executeMethod SamuraiUIValidation.Run -logFile LOG
Unity.exe -batchmode -nographics -projectPath COPY -executeMethod RepairValidation.Run -logFile LOG
Unity.exe -batchmode -nographics -projectPath COPY -executeMethod CombatUpgradeValidation.Run -logFile LOG
```

`SamuraiUIValidation` kiểm tra quyền pause, không mở chồng bảng, mua đúng giá khi
pause, chặn mua thiếu xu, phân vùng HUD và trạng thái kỹ năng. Chụp Game View thật
ở 1280×720, 1920×1080 và 1024×768. Capture chờ hoàn tất khung hình trước khi đóng
bảng để không chụp nhầm trạng thái kế tiếp.

Ảnh: `Logs/UI-2026-10-14/` gồm guide, stats, shrine, HUD và pause.
Log: `Logs/SamuraiUIValidation.log`, `Logs/SamuraiUIRegression.log`,
`Logs/SamuraiUICombatRegression.log`.

## Giới hạn và kiểm tra thủ công

- Đây là trùng tu trên IMGUI hiện có với layout/theme chung, không phải tuyên bố
  đã chuyển toàn bộ dự án sang uGUI/TMP. Canvas máu/mana/coin/mạng góc trên và các
  màn menu/game over vẫn giữ tài nguyên hiện tại.
- Ảnh render và test không thay thế thao tác chuột/bàn phím thật. Kiểm tra F1,
  P, B, Esc liên tục; kéo âm lượng; mua khi vừa đủ/thiếu tiền; đọc số hồi chiêu
  trong trận boss và ở kích thước Game View thường dùng.
- UI có chuỗi số động/IMGUI repaint, chưa phải zero-GC. Không thêm bitmap hoặc
  dependency bên ngoài; style được cache và tìm component HUD theo lượt bind.
- Ảnh/log là artifact local, không phải thay đổi scene hoặc save của người dùng.
