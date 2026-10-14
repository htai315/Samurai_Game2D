namespace Samurai.Core.Presentation
{
    public readonly struct SceneBannerDefinition
    {
        public readonly string Title, Subtitle, Chapter;
        public SceneBannerDefinition(string title, string subtitle, string chapter)
        { Title = title; Subtitle = subtitle; Chapter = chapter; }
    }

    public static class SceneBannerCatalog
    {
        public static bool TryResolve(string sceneName, out SceneBannerDefinition definition)
        {
            definition = default;
            if (string.IsNullOrEmpty(sceneName) || sceneName.Equals("MenuScene", System.StringComparison.OrdinalIgnoreCase)) return false;
            string title, subtitle, chapter;
            switch (sceneName)
            {
                case "SampleScene":
                    title = "U MINH LÂM CẢNH";
                    subtitle = "CHƯƠNG I";
                    chapter = "Lãng khách khởi đầu bước chân phục hận nơi rừng trúc huyền bí";
                    break;
                case "LV2-Scene2":
                    title = "HOANG SƠ HUYỀN HUYỆT";
                    subtitle = "CHƯƠNG II";
                    chapter = "Vực thẳm sâu hun hút ẩn chứa loài quỷ dơi gào thét";
                    break;
                case "Lv3-Scene3":
                    title = "THẠCH QUỶ DI TÍCH";
                    subtitle = "CHƯƠNG III";
                    chapter = "Tàn tích ngàn năm bị phong tỏa bởi người đá khổng lồ";
                    break;
                case "Lv4-Scene4":
                    title = "HỎA MA ĐỊA NGỤC";
                    subtitle = "CHƯƠNG IV";
                    chapter = "Biển lửa dung nham thiêu đốt ý chí kiên định của võ sĩ";
                    break;
                case "LV5-Scene5":
                    title = "NGUYỆT ẢNH THIÊN LỘ";
                    subtitle = "CHƯƠNG V";
                    chapter = "Con đường độc đạo dẫn tới pháo đài kẻ phản bội";
                    break;
                case "LV6-MoonlitCitadel":
                    title = "NGUYỆT QUANG NGUY THÀNH";
                    subtitle = "CHƯƠNG VI";
                    chapter = "Trận quyết chiến định mệnh dưới ánh trăng máu";
                    break;
                case "Ending":
                    title = "KẾT THÚC HÀNH TRÌNH";
                    subtitle = "JOURNEY'S END  •  VÕ ĐẠO VIÊN THÀNH";
                    chapter = "Bóng tối tan biến, bình minh rạng ngời nơi chân trời phương Đông";
                    break;
                default:
                    title = sceneName.ToUpper();
                    subtitle = "SAMURAI CHRONICLES  •  BÍ SỬ VÕ SĨ";
                    chapter = "Tiếp tục bước chân phiêu bạt chốn giang hồ hiểm ác";
                    break;
            }

            definition = new SceneBannerDefinition(title, subtitle, chapter);
            return true;
        }
    }

    public readonly struct LevelBannerSnapshot
    {
        public readonly SceneBannerDefinition Definition;
        public readonly bool Visible;
        public readonly float Alpha;
        public LevelBannerSnapshot(SceneBannerDefinition definition, bool visible, float alpha)
        { Definition = definition; Visible = visible; Alpha = alpha; }
    }

    public sealed class LevelBannerPresentation
    {
        public const float FadeIn = .7f, Stay = 2.8f, FadeOut = .9f;
        public const float Total = FadeIn + Stay + FadeOut;
        public SceneBannerDefinition Definition { get; private set; }
        public float Remaining { get; private set; }
        public void Trigger(string scene)
        {
            bool show = SceneBannerCatalog.TryResolve(scene, out var definition);
            Definition = definition;
            Remaining = show ? Total : 0;
        }
        public void Reset() { Definition = default; Remaining = 0; }
        public void Tick(float delta) { if (Remaining > 0) Remaining -= delta; }
        public LevelBannerSnapshot Capture()
        {
            float alpha = System.Math.Min(Clamp01((Total - Remaining) / FadeIn), Clamp01(Remaining / FadeOut));
            return new LevelBannerSnapshot(Definition, Remaining > 0, alpha);
        }
        private static float Clamp01(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }
}
