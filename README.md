# Samurai_Game2D

Game hành động đi cảnh 2D một người chơi, phong cách samurai và pixel art. Vượt qua
sáu màn, chiến đấu cận chiến, sử dụng kỹ năng, thu thập coin và nâng cấp nhân vật
để tiến tới trận quyết chiến cuối hành trình.

## Tài liệu

- [Hướng dẫn cách chơi](Docs/HUONG_DAN_CHOI.md): phím điều khiển, chiến đấu,
  nâng cấp, checkpoint và lưu tiến trình.
- [Mô tả chi tiết game](Docs/MO_TA_GAME.md): nội dung, các hệ thống gameplay,
  tổ chức mã nguồn và lưu ý cho người phát triển.

## Mở dự án

1. Cài **Unity 6000.0.49f1** qua Unity Hub; đây là phiên bản ghi trong dự án.
2. Add project bằng thư mục chứa `Assets`, `Packages` và `ProjectSettings`.
3. Chờ Unity tải package và import asset xong.
4. Mở `Assets/Scenes/MenuScene.unity`, nhấn Play và bắt đầu từ menu.

Khi đóng gói, kiểm tra danh sách scene trong Build Profiles/Build Settings;
giữ `MenuScene` là màn đầu. Dự án hiện có luồng sáu màn tới `Ending`.
`TutorialScene` tồn tại nhưng đang tắt trong danh sách build.

## Chơi nhanh

**A/D** di chuyển · **K** nhảy · **J** đánh · **L** lướt · **C/Shift trái** parry ·
**U/I** kỹ năng · **Q/O** tuyệt kỹ · **giữ H/E** hồi máu · **F1** hướng dẫn ·
**Esc** tạm dừng.

Source hiện ở `Assets/Scripts`, không phải đường dẫn cũ `Assets/Scrpits`.
Các file JSON trong `Assets/Editor` và `Docs` là fixture/dữ liệu kiểm tra;
không xóa chúng chỉ vì bộ tài liệu Markdown đã được rút gọn.

Không coi test tự động hoặc benchmark ngoài màn hình là chứng nhận game không
còn lỗi hay bảo đảm FPS trên mọi thiết bị. Cần chơi thử bản build thực tế trước
khi phát hành. Giữ thông tin bản quyền/giấy phép đi kèm asset và thư viện bên thứ ba.
