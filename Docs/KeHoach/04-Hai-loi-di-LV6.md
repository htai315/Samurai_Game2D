# Dựng lại đoạn bục thấp của Nguyệt Quang Nguy Thành

**Đã sửa và thử trên Windows Player. Chờ xác minh import bằng Unity Editor.** Chọn đoạn LV6 có lối dưới từng bị vướng đầu và một dãy bục phía trên. Tuyến dưới dành cho đi bộ và giao chiến. Tuyến trên đòi hỏi nhảy chính xác hơn và cho người chơi thấy trước phần thưởng hoặc vị trí đáp có lợi.

## Các bước thực hiện

- [x] Mở LV6-MoonlitCitadel, đối chiếu ảnh phản hồi với scene hiện tại. Ghi camera, vị trí bắt đầu/kết thúc và tên object hoặc ô tile của từng bục liên quan. So sánh scene với CitadelMapBuilder và CitadelRenovation để giữ các chỉnh sửa thủ công hiện có.
- [x] Dùng level-design đo bằng prefab player ở chỉ số cơ bản. Ghi chiều cao collider đứng, chiều cao thông thủy tại điểm thấp nhất, quãng nhảy ngắn/dài, độ cao mặt đáp và bề rộng tiếp đất. Đo bằng đường đi thật từ cả hai phía.
- [x] Làm bản khối xám ở riêng đoạn mẫu. Tuyến dưới cần đủ chiều cao suốt quãng đi, kể cả mối nối collider giữa hai tile. Tuyến trên có điểm lấy đà, nhìn được chỗ đáp đầu tiên, và có chỗ quay lại nếu hụt. Đặt điểm nhập hai tuyến nơi camera đã cho thấy đoạn giao tranh tiếp theo.
- [x] Chạy thử khi chưa mua Phong ngọc hoặc nâng tốc độ, rồi chạy lại với các nâng cấp đó. Kiểm tra khi đổi hướng dưới bục, nhảy đụng mép, đáp sát góc và hồi sinh ở checkpoint liên quan.
- [x] Sau khi hai đường đi ổn, tham khảo cách xếp lớp tiền cảnh, nền và điểm sáng ở màn III/IV. Chọn một cụm đèn dẫn mắt tới điểm đáp, giữ mép va chạm nhìn rõ. Chừa khoảng nhìn cho [cảnh mở trận cuối](02-Dau-kiem-sau-man.md) nếu hai đoạn cùng lọt vào một khung hình.
- [ ] Chạy CitadelTraversalValidation và CitadelValidation trên bản sao. Kiểm tra luồng LV5 → LV6 → Ending, thử rơi khỏi nhánh trên và đi tiếp từ checkpoint. Nếu cần chỉnh công cụ dựng màn, tái tạo thử trên bản sao rồi so sánh riêng vùng sửa.

## Tiêu chí duyệt

| Lượt thử | Kết quả cần lưu |
|---|---|
| Đi bộ hai chiều ở tuyến dưới | Video chạy liên tục, không kẹt đầu hoặc mắc ở mối nối. |
| Player cơ bản chọn tuyến trên | Nhìn được phần thưởng hoặc lợi thế ngay từ ngã rẽ và đáp được qua từng bục. |
| Cố ý nhảy hụt | Trở lại được tuyến chính hoặc checkpoint bằng điều khiển bình thường. |
| Chạy ở kích thước Game View thường dùng | Phân biệt nền trang trí với mép có thể đứng, kể cả khi hiệu ứng combat xuất hiện. |

Bàn giao ảnh trước/sau cùng vị trí camera và bảng kích thước đo trong Unity. Nếu có thay đổi C# gameplay, dựng lại Graphify sau khi xác minh.

## Kết quả tại hành lang kiếm, Route 13

Chọn Optional balcony 13 theo dấu hiệu kiếm và ba đồng xu. Nền ở Y=8. Collider player đo được rộng 0,96 và cao 2,06 đơn vị. Trước sửa đã đi dưới được cả hai chiều. Khoảng hở tăng 3,00 → 3,20, bề rộng mặt đáp tăng 6,00 → 6,75, đỉnh giữ ở Y=11,50. Tâm bục dịch X=269 → 269,75. Nhảy ngắn và giữ phím tại chỗ đạt 1,23 và 3,90 đơn vị tính từ nền. Bài nhảy vào mép đài đi ngang khoảng 2,69 đơn vị đến lúc đáp. Giãn ba xu quanh kiếm và đặt đèn ở cuối bục để đánh dấu lối trên. ID vật phẩm giữ nguyên.

Player thử bằng input thật trên hình học mới áp dụng trong bản đóng gói cũ: đi hai hướng, đổi hướng dưới dầm, nhảy một lần từ hai phía, chạm trần và đáp lại sàn, di chuyển nhanh khi có Phong ngọc. Các ca đo hình học cô lập địch. Luồng LV5 → LV6 → checkpoint → Save/Continue → hồi sinh → PlayAgain → boss → Ending đạt trên bản chạy Windows. Hình trước/sau cùng camera và GIF: Logs/LV6-Lanes-before.png, Logs/LV6-Lanes-after.png, Logs/LV6-Lanes-walk.gif. Chưa đánh giá cảm giác trong trận đánh thật. Hai runner Editor đều dừng tại bước license trước khi mở project.
