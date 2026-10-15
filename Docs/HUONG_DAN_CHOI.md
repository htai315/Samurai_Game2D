# Hướng dẫn chơi Samurai_Game2D

## 1. Mục tiêu và bắt đầu

Bạn điều khiển một samurai vượt địa hình, chiến đấu với quái và tiến qua sáu màn
để đến đoạn kết. Thu thập coin, giữ máu và mana, tận dụng điểm nghỉ/checkpoint
thay vì chỉ lao vào đánh liên tục.

Trong menu, chọn bắt đầu lượt mới hoặc tiếp tục nếu đã có save hợp lệ. Khi chơi
trong Unity, nên mở `MenuScene` trước để trải nghiệm đúng luồng menu và tiến trình.

## 2. Phím điều khiển

| Phím | Chức năng |
| --- | --- |
| A / D hoặc ← / → | Di chuyển, đổi hướng nhìn |
| K | Nhảy; có lần nhảy thứ hai khi trên không |
| Nhả K sớm | Giảm độ cao cú nhảy |
| J | Đánh cận chiến, nối chuỗi ba đòn |
| W hoặc ↑ + J | Đòn chém hướng lên |
| S hoặc ↓ + J | Đòn chém bổ; trên không có thể lao xuống |
| L | Lướt né theo hướng nhìn, tiêu hao mana |
| C hoặc Shift trái | Parry/đỡ đúng thời điểm |
| U | Phóng kiếm khí |
| I | Thi triển kỹ năng lửa |
| Q hoặc O | Tuyệt kỹ Nhất Kiếm Đoạt Mệnh |
| Giữ H hoặc E | Tĩnh tâm: dùng mana để hồi máu khi đủ điều kiện |
| P | Mở/đóng bảng thuộc tính, nâng cấp và hộ phù |
| B | Mở/đóng giao diện đền thờ/hộ phù |
| W khi gần điểm nghỉ | Tương tác giao diện đền thờ; tránh nhầm với tổ hợp chém lên |
| 1 / 2 / 3 khi bảng mua đang mở | Mua nhanh các hộ phù tương ứng |
| F1 | Mở/đóng hướng dẫn trong game |
| Esc | Tạm dừng/cài đặt; đóng bảng đang mở trước |
| F5 | Lưu thủ công qua adapter hiện có trên player |

Phím ở trên đối chiếu với code hiện tại. Hành động chỉ thực hiện khi nhân vật
đủ điều kiện: còn sống, không bị khóa bởi hành động khác, đủ mana và hết hồi chiêu.
Giữ phím đánh không đồng nghĩa tự động đánh liên tục.

## 3. Di chuyển và vượt địa hình

- Nhấn K ngắn để nhảy thấp; giữ lâu hơn để tận dụng độ cao của cú nhảy.
- Nhấn K lần nữa khi đang trên không để dùng lần nhảy còn lại.
- Khi rơi sát vách có thể trượt tường; nhảy để đạp khỏi vách khi trạng thái cho phép.
- Coyote time và jump buffer hỗ trợ những lần bấm nhảy hơi lệch thời điểm.
- Lướt giúp né và vượt khoảng cách ngắn nhưng không xuyên qua địa hình chắn.
- Rơi khỏi giới hạn màn là nguy hiểm; miễn sát thương khi dash không bảo vệ khỏi
  cơ chế tử vong môi trường.

## 4. Chiến đấu

### Cận chiến và nhịp nối đòn

Nhấn J để đánh theo hướng đang nhìn. Các lần nhấn liên tiếp phù hợp sẽ nối chuỗi
ba đòn; W/↑ hoặc S/↓ thay đổi kiểu đánh. S/↓ + J khi trên không có thể kích hoạt
đòn lao xuống; đòn bổ trên không có cơ chế nảy khi đánh trúng trong điều kiện phù hợp.

Bạn có thể dash để ngắt đòn cận chiến. Sau khi sự kiện chém đã phát, phần đuôi
animation có thể nhường cho di chuyển hoặc lệnh mới; không thể bỏ qua phần chuẩn
bị để gây damage ngay. Buffer ngắn hỗ trợ lệnh bấm hơi sớm, không phải hàng đợi
tự đánh. Đừng bấm liên tục nhiều kỹ năng và kỳ vọng tất cả cùng thực hiện.

### Né và parry

L dùng mana để lướt, tránh sát thương thường trong thời gian dash. C/Shift trái
mở cửa sổ parry ngắn: căn sát lúc đòn địch tới thay vì giữ nút. Parry thành công
có phản hồi kim loại, hỗ trợ làm choáng/phản đòn và hồi mana theo cấu hình.
Khả năng phản xạ phụ thuộc loại đòn; không coi mọi hiểm họa là có thể parry.

### Kỹ năng và tuyệt kỹ

U dùng kiếm khí, I dùng lửa. Q/O kích hoạt tuyệt kỹ tụ kiếm, lướt qua đường đánh,
đánh dấu mục tiêu hợp lệ rồi bùng nổ vết chém. Địa hình vẫn cản đường/khả năng
nhận mục tiêu; chiêu không cho phép đánh xuyên mọi bức tường.

Mana và hồi chiêu giới hạn việc dùng kỹ năng. Tuyệt kỹ hiện dùng mana và cooldown,
không có yêu cầu tích thanh nộ. Theo dõi trạng thái kỹ năng trên HUD.

### Hồi máu

Giữ H/E khi đứng trên đất, thiếu máu và đủ mana để tĩnh tâm. Cần giữ đủ thời gian
cho mỗi nhịp hồi; nhả nút hoặc bị ngắt sẽ dừng niệm. Chọn vị trí an toàn, vì hồi
máu không đồng nghĩa bất tử.

## 5. Tài nguyên và nâng cấp

HUD cho biết máu, mana, coin, số mạng và trạng thái kỹ năng; combo phản ánh những
lần đánh được xác nhận thành công. Boss có thanh máu riêng.

Coin dùng mua nâng cấp máu tối đa, mana tối đa, sát thương và tốc độ, hoặc mở
hộ phù lửa/gió/hút coin. Mở bảng P và đọc tác dụng/giá đang hiển thị trước khi mua.
Chỉ mua được khi đủ điều kiện và đủ coin; hộ phù đã sở hữu không phải mua lại.
Giá và hiệu lực có thể khác theo cấu hình prefab/scene, nên tài liệu không ấn định
một bảng giá thay cho giao diện game.

Vật phẩm trên đường có thể hồi máu, thêm mạng hoặc tăng sức mạnh kiếm. Buff kiếm
có thể vĩnh viễn hoặc có thời hạn tùy vật phẩm. Một số vật thể có thể bị phá để
nhận thưởng; không phải mọi vật trang trí đều phá được.

## 6. Checkpoint, chết và lưu game

- Kích hoạt checkpoint/điểm nghỉ trên đường để hỗ trợ hồi sinh trong màn.
- Khi chết, hệ thống mạng và respawn quyết định tiếp tục tại checkpoint hay mở Game Over.
- **Play Again** từ Game Over dùng trạng thái đầu màn được ghi nhận, không mặc định
  giữ toàn bộ lợi ích đã nhặt hoặc mua sau mốc đó. Các tình huống hồi sinh/checkpoint
  có chính sách khôi phục riêng; checkpoint không đồng nghĩa đã ghi save xuống đĩa.
- Dùng **Esc → Lưu và về menu** để lưu rồi rời lượt chơi. F5 là đường lưu thủ công
  bổ sung; kiểm tra thông báo nếu nghi lưu thất bại.
- **Continue** nạp snapshot đã lưu. Save gồm màn/vị trí, chỉ số, máu/mana, coin,
  mạng, hộ phù, pickup đã thu thập và mốc đầu màn khi có.
- Không dựa vào việc đóng cưỡng bức ứng dụng để lưu. Bắt đầu lượt mới có thể reset
  tiến trình theo luồng menu; kiểm tra lựa chọn trước khi xác nhận.

## 7. Mẹo chơi và khi thao tác không như mong đợi

Giữ khoảng cách, quan sát nhịp địch, dùng parry để lấy lại lợi thế rồi mới tung
combo. Dành mana cho cả né và hồi máu, không dồn hết vào kỹ năng tấn công.

Nếu nhân vật không ra chiêu, kiểm tra mana, hồi chiêu, trạng thái hurt và bảng pause.
Một nhịp chậm ngắn lúc trúng đòn có thể là hit-stop có chủ đích. Nếu toàn khung
hình giật, so sánh với bản build, ghi lại màn/tình huống và thời điểm để đo Profiler;
không mặc định mọi cú khựng đều do thiếu FPS.
