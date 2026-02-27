using HeriStepAI.Shared.Interfaces;
using HeriStepAI.Shared.Models;

namespace HeriStepAI.Mobile.Services;

/// <summary>
/// Service tạo dữ liệu mẫu các điểm di tích để hiển thị trên bản đồ
/// Sẽ chỉ seed 1 lần khi database trống
/// </summary>
public static class SeedDataService
{
    public static async Task SeedIfEmptyAsync(IPOIRepository poiRepository, ITourRepository tourRepository)
    {
        var existingPOIs = await poiRepository.GetAllAsync();
        if (existingPOIs.Count > 0)
            return; // Đã có dữ liệu, không seed

        System.Diagnostics.Debug.WriteLine("[SeedData] Database trống, đang thêm dữ liệu mẫu...");

        // === TOUR 1: Di tích lịch sử TP.HCM ===
        var tour1 = new Tour
        {
            Id = 1,
            Name = "Di tích lịch sử TP.HCM",
            Description = "Tour tham quan các di tích lịch sử nổi tiếng tại Thành phố Hồ Chí Minh",
            UniqueCode = "TOUR_HCM_01",
            EstimatedDurationMinutes = 180,
            EstimatedDistanceMeters = 5000,
            POICount = 7,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await tourRepository.InsertAsync(tour1);

        // === TOUR 2: Di tích Huế ===
        var tour2 = new Tour
        {
            Id = 2,
            Name = "Cố đô Huế",
            Description = "Tour tham quan các di tích cố đô Huế - Di sản văn hóa thế giới",
            UniqueCode = "TOUR_HUE_01",
            EstimatedDurationMinutes = 240,
            EstimatedDistanceMeters = 15000,
            POICount = 3,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await tourRepository.InsertAsync(tour2);

        // === CÁC ĐIỂM DI TÍCH TP.HCM ===
        var hcmPOIs = new List<POI>
        {
            new POI
            {
                Id = 1,
                UniqueCode = "HCM_DINHDOCLAP",
                Name = "Dinh Độc Lập",
                ShortDescription = "Dinh Thống Nhất - biểu tượng lịch sử Việt Nam",
                FullDescription = "Dinh Độc Lập (hay còn gọi là Dinh Thống Nhất) là một công trình kiến trúc nổi tiếng tại Thành phố Hồ Chí Minh. Đây là nơi làm việc của Tổng thống Việt Nam Cộng hòa trước năm 1975. Ngày 30 tháng 4 năm 1975, xe tăng của Quân đội Nhân dân Việt Nam đã húc đổ cổng chính, đánh dấu sự kiện thống nhất đất nước. Dinh được xây dựng trên diện tích 120.000 m², thiết kế bởi kiến trúc sư Ngô Viết Thụ.",
                TTSScript = "Chào mừng bạn đến với Dinh Độc Lập, còn gọi là Dinh Thống Nhất. Đây là công trình kiến trúc nổi tiếng, biểu tượng lịch sử của Việt Nam. Dinh được xây dựng năm 1966 bởi kiến trúc sư Ngô Viết Thụ. Ngày 30 tháng 4 năm 1975, xe tăng đã húc đổ cổng chính, đánh dấu ngày thống nhất đất nước.",
                Latitude = 10.7769,
                Longitude = 106.6951,
                TriggerRadius = 100,
                ApproachRadius = 200,
                Priority = 10,
                TourId = 1,
                OrderInTour = 1,
                Language = "vi-VN",
                IsActive = true,
                CooldownSeconds = 300
            },
            new POI
            {
                Id = 2,
                UniqueCode = "HCM_NHATHODUCBA",
                Name = "Nhà thờ Đức Bà",
                ShortDescription = "Vương cung thánh đường chính tòa Đức Mẹ Vô Nhiễm Nguyên Tội",
                FullDescription = "Nhà thờ Đức Bà Sài Gòn, tên chính thức là Vương cung thánh đường chính tòa Đức Mẹ Vô Nhiễm Nguyên Tội, là nhà thờ lớn nhất và nổi tiếng nhất tại Thành phố Hồ Chí Minh. Được xây dựng từ năm 1863 đến 1880, nhà thờ mang phong cách kiến trúc Roman kết hợp Gothic, với hai tháp chuông cao 57,6 mét.",
                TTSScript = "Bạn đang đứng trước Nhà thờ Đức Bà Sài Gòn. Công trình được xây dựng từ năm 1863, hoàn thành năm 1880, mang phong cách kiến trúc Roman kết hợp Gothic. Hai tháp chuông cao 57 phẩy 6 mét là điểm nhấn nổi bật. Tất cả vật liệu xây dựng đều được nhập từ Pháp.",
                Latitude = 10.7798,
                Longitude = 106.6990,
                TriggerRadius = 80,
                ApproachRadius = 150,
                Priority = 9,
                TourId = 1,
                OrderInTour = 2,
                Language = "vi-VN",
                IsActive = true,
                CooldownSeconds = 300
            },
            new POI
            {
                Id = 3,
                UniqueCode = "HCM_BUUDIEN",
                Name = "Bưu điện Trung tâm Sài Gòn",
                ShortDescription = "Công trình kiến trúc Pháp tiêu biểu, xây dựng 1886-1891",
                FullDescription = "Bưu điện Trung tâm Sài Gòn là một trong những công trình kiến trúc tiêu biểu tại TP.HCM, được xây dựng trong khoảng 1886-1891 theo đồ án thiết kế của kiến trúc sư Alfred Foulhoux. Bưu điện mang phong cách kiến trúc châu Âu kết hợp nét trang trí phương Đông, với trần vòm cao, cửa kính màu và bản đồ Sài Gòn xưa.",
                TTSScript = "Đây là Bưu điện Trung tâm Sài Gòn, được xây dựng từ năm 1886 đến 1891, thiết kế bởi kiến trúc sư Alfred Foulhoux. Công trình mang phong cách kiến trúc châu Âu kết hợp nét trang trí phương Đông. Bên trong có bức chân dung Chủ tịch Hồ Chí Minh và các bản đồ Sài Gòn cổ.",
                Latitude = 10.7800,
                Longitude = 106.6999,
                TriggerRadius = 60,
                ApproachRadius = 120,
                Priority = 8,
                TourId = 1,
                OrderInTour = 3,
                Language = "vi-VN",
                IsActive = true,
                CooldownSeconds = 300
            },
            new POI
            {
                Id = 4,
                UniqueCode = "HCM_CHOBENTTHANH",
                Name = "Chợ Bến Thành",
                ShortDescription = "Biểu tượng của Sài Gòn từ đầu thế kỷ 20",
                FullDescription = "Chợ Bến Thành là ngôi chợ nổi tiếng nhất Sài Gòn, được xây dựng năm 1912-1914. Chợ nằm ở trung tâm Quận 1, là biểu tượng không chính thức của thành phố. Chợ có diện tích hơn 13.000 m² với hơn 3.000 sạp hàng, kinh doanh đa dạng từ thực phẩm, quần áo đến đồ lưu niệm.",
                TTSScript = "Chào mừng bạn đến Chợ Bến Thành, biểu tượng nổi tiếng của Sài Gòn. Chợ được xây dựng từ năm 1912 đến 1914, với diện tích hơn 13 nghìn mét vuông. Đây là nơi lý tưởng để trải nghiệm ẩm thực đường phố và mua sắm đồ lưu niệm.",
                Latitude = 10.7725,
                Longitude = 106.6980,
                TriggerRadius = 100,
                ApproachRadius = 200,
                Priority = 8,
                TourId = 1,
                OrderInTour = 4,
                Language = "vi-VN",
                IsActive = true,
                CooldownSeconds = 300
            },
            new POI
            {
                Id = 5,
                UniqueCode = "HCM_BAOCHIENCHUNG",
                Name = "Bảo tàng Chứng tích Chiến tranh",
                ShortDescription = "Bảo tàng về chiến tranh Việt Nam, thu hút hàng triệu du khách",
                FullDescription = "Bảo tàng Chứng tích Chiến tranh là một trong những bảo tàng được tham quan nhiều nhất tại Việt Nam, nằm tại Quận 3, TP.HCM. Bảo tàng trưng bày các hiện vật, hình ảnh và tài liệu về chiến tranh Việt Nam, đặc biệt là tác động của chất độc da cam và bom mìn. Bảo tàng thu hút hơn 1 triệu lượt khách mỗi năm.",
                TTSScript = "Đây là Bảo tàng Chứng tích Chiến tranh, một trong những bảo tàng được tham quan nhiều nhất Việt Nam. Bảo tàng trưng bày hình ảnh và hiện vật về chiến tranh Việt Nam. Mỗi năm có hơn 1 triệu lượt khách đến tham quan.",
                Latitude = 10.7794,
                Longitude = 106.6920,
                TriggerRadius = 80,
                ApproachRadius = 150,
                Priority = 9,
                TourId = 1,
                OrderInTour = 5,
                Language = "vi-VN",
                IsActive = true,
                CooldownSeconds = 300
            },
            new POI
            {
                Id = 6,
                UniqueCode = "HCM_UBND",
                Name = "UBND Thành phố Hồ Chí Minh",
                ShortDescription = "Trụ sở Hội đồng Nhân dân & UBND TP.HCM, kiến trúc Pháp",
                FullDescription = "Trụ sở UBND Thành phố Hồ Chí Minh (nguyên Tòa Đô Chánh Sài Gòn) được xây dựng năm 1898-1909, thiết kế bởi kiến trúc sư Gardès theo phong cách kiến trúc Pháp thời kỳ Phục Hưng. Phía trước là tượng đài Chủ tịch Hồ Chí Minh và đường Nguyễn Huệ - phố đi bộ nổi tiếng.",
                TTSScript = "Trước mặt bạn là trụ sở Ủy ban Nhân dân Thành phố Hồ Chí Minh. Tòa nhà được xây dựng từ năm 1898 đến 1909, mang phong cách kiến trúc Pháp thời Phục Hưng. Phía trước là tượng đài Bác Hồ và phố đi bộ Nguyễn Huệ.",
                Latitude = 10.7763,
                Longitude = 106.7009,
                TriggerRadius = 80,
                ApproachRadius = 150,
                Priority = 7,
                TourId = 1,
                OrderInTour = 6,
                Language = "vi-VN",
                IsActive = true,
                CooldownSeconds = 300
            },
            new POI
            {
                Id = 7,
                UniqueCode = "HCM_NHAHAT",
                Name = "Nhà hát Thành phố",
                ShortDescription = "Nhà hát lớn Sài Gòn, kiến trúc Pháp đặc trưng",
                FullDescription = "Nhà hát Thành phố Hồ Chí Minh (Nhà hát Lớn) được xây dựng năm 1897 theo phong cách kiến trúc Flamboyant của Pháp. Nhà hát có sức chứa 468 ghế, là nơi diễn ra các sự kiện văn hóa nghệ thuật quan trọng. Mặt tiền được trang trí tinh xảo với các bức phù điêu và tượng.",
                TTSScript = "Nhà hát Thành phố, hay Nhà hát Lớn Sài Gòn, được xây dựng năm 1897. Công trình mang phong cách kiến trúc Flamboyant đặc trưng của Pháp, với sức chứa 468 chỗ ngồi. Mặt tiền được trang trí rất tinh xảo.",
                Latitude = 10.7766,
                Longitude = 106.7032,
                TriggerRadius = 60,
                ApproachRadius = 120,
                Priority = 7,
                TourId = 1,
                OrderInTour = 7,
                Language = "vi-VN",
                IsActive = true,
                CooldownSeconds = 300
            }
        };

        // === CÁC ĐIỂM DI TÍCH HUẾ ===
        var huePOIs = new List<POI>
        {
            new POI
            {
                Id = 8,
                UniqueCode = "HUE_DAINOI",
                Name = "Đại Nội Huế",
                ShortDescription = "Hoàng thành Huế - Di sản Văn hóa Thế giới UNESCO",
                FullDescription = "Đại Nội Huế (Hoàng thành Huế) là quần thể di tích cố đô Huế, được UNESCO công nhận là Di sản Văn hóa Thế giới năm 1993. Đại Nội có diện tích khoảng 520 ha, bao gồm Kinh thành, Hoàng thành, Tử Cấm thành với hàng trăm công trình kiến trúc cung đình. Đây là nơi sinh hoạt của triều đình nhà Nguyễn (1802-1945).",
                TTSScript = "Chào mừng bạn đến Đại Nội Huế, Di sản Văn hóa Thế giới UNESCO từ năm 1993. Quần thể rộng 520 héc ta, gồm Kinh thành, Hoàng thành và Tử Cấm thành. Đây là nơi sinh hoạt của triều đình nhà Nguyễn trong suốt 143 năm.",
                Latitude = 16.4698,
                Longitude = 107.5784,
                TriggerRadius = 200,
                ApproachRadius = 500,
                Priority = 10,
                TourId = 2,
                OrderInTour = 1,
                Language = "vi-VN",
                IsActive = true,
                CooldownSeconds = 300
            },
            new POI
            {
                Id = 9,
                UniqueCode = "HUE_CHUATHIENMU",
                Name = "Chùa Thiên Mụ",
                ShortDescription = "Ngôi chùa cổ nhất và đẹp nhất Huế, bên bờ sông Hương",
                FullDescription = "Chùa Thiên Mụ (hay Linh Mụ) tọa lạc trên đồi Hà Khê, bên bờ sông Hương. Chùa được xây dựng năm 1601 bởi chúa Nguyễn Hoàng. Tháp Phước Duyên cao 21 mét với 7 tầng là biểu tượng nổi tiếng nhất của chùa và cũng là biểu tượng không chính thức của Huế.",
                TTSScript = "Chùa Thiên Mụ tọa lạc trên đồi Hà Khê, bên dòng sông Hương thơ mộng. Chùa được xây dựng năm 1601. Tháp Phước Duyên 7 tầng cao 21 mét là biểu tượng nổi tiếng nhất, đại diện cho vẻ đẹp cổ kính của Huế.",
                Latitude = 16.4539,
                Longitude = 107.5510,
                TriggerRadius = 100,
                ApproachRadius = 200,
                Priority = 9,
                TourId = 2,
                OrderInTour = 2,
                Language = "vi-VN",
                IsActive = true,
                CooldownSeconds = 300
            },
            new POI
            {
                Id = 10,
                UniqueCode = "HUE_LANGKHAIDINH",
                Name = "Lăng Khải Định",
                ShortDescription = "Lăng mộ vua Khải Định - kiến trúc độc đáo Đông Tây kết hợp",
                FullDescription = "Lăng Khải Định là lăng mộ của vua Khải Định, vị vua thứ 12 của triều Nguyễn. Lăng được xây dựng từ 1920 đến 1931, mang phong cách kiến trúc độc đáo kết hợp Đông và Tây. Nổi bật nhất là cung Thiên Định với nội thất trang trí bằng kính và sứ ghép hình cực kỳ tinh xảo.",
                TTSScript = "Lăng Khải Định được xây dựng từ năm 1920 đến 1931, là lăng mộ của vua Khải Định, vị vua thứ 12 triều Nguyễn. Kiến trúc kết hợp phong cách Đông và Tây rất độc đáo. Điểm nhấn là cung Thiên Định với nội thất ghép kính và sứ tuyệt đẹp.",
                Latitude = 16.3930,
                Longitude = 107.5893,
                TriggerRadius = 100,
                ApproachRadius = 200,
                Priority = 8,
                TourId = 2,
                OrderInTour = 3,
                Language = "vi-VN",
                IsActive = true,
                CooldownSeconds = 300
            }
        };

        // Insert tất cả POIs
        foreach (var poi in hcmPOIs)
        {
            await poiRepository.InsertAsync(poi);
        }

        foreach (var poi in huePOIs)
        {
            await poiRepository.InsertAsync(poi);
        }

        var totalPOIs = hcmPOIs.Count + huePOIs.Count;
        System.Diagnostics.Debug.WriteLine($"[SeedData] Đã thêm {totalPOIs} điểm thuyết minh và 2 tour");
    }
}
