# Kể chuyện qua hai vết kiếm từ màn I đến màn VI

**Đã dựng dấu kiếm ở màn I; chờ playtest nhận diện.** Người chơi lần theo một kiếm sĩ dùng cùng kiếm pháp. Dấu nhận diện là hai vết cắt giao nhau, nhát thứ hai ngắn hơn. Chi tiết trên tuyến chính cần đủ để hiểu màn đối đầu. Người thích khám phá có thêm manh mối ở tuyến phụ.

## Công việc theo màn

| Màn | Việc cần dựng | Điều kiểm tra khi chơi |
|---|---|---|
| I · SampleScene | Sau hai trận ở cầu, đặt dấu cắt trên mặt gỗ hướng về camera. Chọn một cạnh sáng để vết cắt đọc được trên nền rừng. | Khi rời trận, cột nằm trong tầm nhìn theo hướng đi tự nhiên. |
| II · LV2-Scene2 | Dùng lại dấu ở giá đèn gãy bên đường phụ. Cho thấy một phần phần thưởng từ ngã rẽ. | Có lý do để rẽ và có đường trở lại. Bỏ qua vẫn đến được cổng. |
| III · Lv3-Scene3 | Đặt phù điêu hai kiếm sĩ cùng vào thế ở một chỗ nghỉ trên đường chính. Một gương mặt bị đục đi. Khảo sát điểm nghỉ thực tế trước khi đặt. | Khi dừng lại, player nhìn thấy thế tay và hướng kiếm ở độ phân giải chơi bình thường. |
| IV · Lv4-Scene4 | Đặt cửa cháy với then bị chém từ trong. Bố trí góc nhìn lúc player vừa đi qua cửa. | Có thể quay lại nhìn và hiểu phía nào của then bị phá. |
| V · LV5-Scene5 | Một bóng kiếm sĩ trên mái thực hiện hai nhát quen thuộc rồi khuất sau cổng. Kích hoạt ở đoạn đất bằng. | Player đang chạy vẫn thấy được hai động tác và nhìn rõ chỗ đặt chân tiếp theo. |
| VI · LV6-MoonlitCitadel | Tại cuộc đối đầu cuối, dùng tư thế trên phù điêu và thứ tự hai nhát làm tín hiệu mở trận. Xác minh boss và điểm kích hoạt thực tế trong scene. | Người đã để ý nhận ra dấu kiếm. Người mới vẫn đọc được nhịp né của cả hai nhát. |

## Trình tự sản xuất

- [ ] Dùng narrative-design phác một bộ ba hình cùng tỉ lệ pixel: dấu trên gỗ, dáng trên đá, dáng boss. Kiểm tra chúng cạnh sprite player ở kích thước Game View.
- [ ] Dùng level-design dựng thử màn I cùng [hai trận ở cầu](01-Giao-chien-Sample.md). Quan sát một người chơi lần đầu, không chỉ trước vị trí cột.
- [ ] Sau khi hình dấu kiếm đọc được, triển khai II–V theo bảng. Trang trí để ở layer phù hợp, kiểm tra đường chạy, collider, camera và việc load lại checkpoint. Cảnh bóng người chỉ phát một lần trong một lượt đi qua khu vực.
- [ ] Với màn VI, xem BossAbilityController, BossHealth và clip boss trước khi chọn điểm gắn động tác. Tách dấu báo khỏi thời điểm nhận sát thương. Thử lại khi bị ngắt, chết và bắt đầu lại trận.
- [ ] Chơi xuyên sáu màn. Sau màn III hỏi “Bạn nghĩ ai từng đi qua đây?”. Sau trận cuối hỏi “Bạn đã thấy thế kiếm ấy ở đâu?”. Ghi nguyên câu trả lời và chi tiết bị bỏ lỡ để chọn vị trí cần sửa.

**Bàn giao:** Sáu ảnh Game View theo thứ tự, một đoạn quay từ lúc vào đấu trường tới khi né được hai nhát, và ghi chú playtest. Cập nhật Graphify nếu có thay đổi mã điều khiển boss hoặc kích hoạt cảnh.
