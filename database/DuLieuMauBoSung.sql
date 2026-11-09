SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @NhanSu TABLE (SoThuTu int, HoTen nvarchar(100), TenChucVu nvarchar(100));
INSERT INTO @NhanSu VALUES
    (1, N'Nguyễn Hoàng Minh', N'Thợ sửa máy chuyên sâu'),
    (2, N'Trần Quốc Huy', N'Thợ điện xe máy'),
    (3, N'Lê Anh Tuấn', N'Thợ bảo dưỡng định kỳ'),
    (4, N'Phạm Đức Long', N'Thợ kiểm tra xe'),
    (5, N'Hoàng Văn Nam', N'Thợ lốp và phanh'),
    (6, N'Đặng Minh Khang', N'Thợ khung gầm xe máy'),
    (7, N'Vũ Thanh Tùng', N'Thợ chuyên xe tay ga'),
    (8, N'Bùi Gia Bảo', N'Thợ chuyên xe số'),
    (9, N'Đỗ Nhật Quang', N'Thợ chuyên xe côn tay'),
    (10, N'Nguyễn Thu Hà', N'Thu ngân ca sáng'),
    (11, N'Trần Ngọc Mai', N'Thu ngân ca chiều'),
    (12, N'Lê Phương Linh', N'Quản lý ca cửa hàng');

DECLARE @Khach TABLE (SoThuTu int, HoTen nvarchar(100));
INSERT INTO @Khach VALUES
    (1, N'Nguyễn Hải Đăng'), (2, N'Trần Bảo Ngọc'), (3, N'Lê Minh Châu'),
    (4, N'Phạm Tuấn Kiệt'), (5, N'Hoàng Thanh Trúc'), (6, N'Đặng Quốc Bảo'),
    (7, N'Vũ Khánh Linh'), (8, N'Bùi Đức Anh'), (9, N'Đỗ Thảo Nguyên'),
    (10, N'Nguyễn Gia Hân'), (11, N'Trần Nhật Minh'), (12, N'Lê Ngọc Anh');

DECLARE @DichVuMau TABLE (SoThuTu int, TenDichVu nvarchar(150), TenPhuTung nvarchar(150));
INSERT INTO @DichVuMau VALUES
    (1, N'Thay dầu động cơ', N'Dầu động cơ 10W-40'),
    (2, N'Kiểm tra và thay má phanh', N'Má phanh đĩa trước'),
    (3, N'Thay lọc gió động cơ', N'Lọc gió xe máy'),
    (4, N'Vệ sinh kim phun', N'Dung dịch vệ sinh kim phun'),
    (5, N'Kiểm tra và thay bugi', N'Bugi tiêu chuẩn'),
    (6, N'Bảo dưỡng bộ truyền động', N'Mỡ bôi trơn truyền động'),
    (7, N'Thay nước làm mát động cơ', N'Nước làm mát xe máy'),
    (8, N'Kiểm tra và thay bình ắc quy', N'Ắc quy xe máy 12V'),
    (9, N'Cân chỉnh và thay vòng bi bánh xe', N'Vòng bi bánh xe'),
    (10, N'Vệ sinh buồng đốt động cơ', N'Dung dịch vệ sinh buồng đốt'),
    (11, N'Kiểm tra hệ thống điện và cầu chì', N'Cầu chì xe máy 10A'),
    (12, N'Bảo dưỡng tổng quát xe máy', N'Bộ gioăng bảo dưỡng');

DECLARE @HomNay date = CONVERT(date, DATEADD(hour, 7, SYSUTCDATETIME()));
DECLARE @MatKhau varchar(64) = CONVERT(varchar(64), HASHBYTES('SHA2_256', '123456'), 2);
DECLARE @SoThuTu int = 1;

WHILE @SoThuTu <= 12
BEGIN
    DECLARE @DuoiMa varchar(5) = RIGHT('00000' + CONVERT(varchar(5), @SoThuTu), 5);
    DECLARE @MaChucVu varchar(10) = 'CVMAU' + @DuoiMa;
    DECLARE @MaKhoan varchar(10) = 'KTMAU' + @DuoiMa;
    DECLARE @MaNhanVien varchar(10) = 'NVMAU' + @DuoiMa;
    DECLARE @MaCa varchar(10) = 'CAMAU' + @DuoiMa;
    DECLARE @MaKhach varchar(10) = 'KHMAU' + @DuoiMa;
    DECLARE @MaXe varchar(10) = 'XEMAU' + @DuoiMa;
    DECLARE @MaNhaCungCap varchar(10) = 'NCCMA' + @DuoiMa;
    DECLARE @MaPhuTung varchar(10) = 'PTMAU' + @DuoiMa;
    DECLARE @MaDichVu varchar(10) = 'DVMAU' + @DuoiMa;
    DECLARE @MaPhieuNhap varchar(10) = 'PNMAU' + @DuoiMa;
    DECLARE @MaDieuChinh varchar(10) = 'DCMAU' + @DuoiMa;
    DECLARE @MaDieuChinhGia varchar(10) = 'DGMAU' + @DuoiMa;
    DECLARE @MaPhieuSua varchar(10) = 'TNMAU' + @DuoiMa;
    DECLARE @MaHoaDon varchar(10) = 'HDMAU' + @DuoiMa;
    DECLARE @MaLichHen varchar(24) = 'LH-MAU' + @DuoiMa;
    DECLARE @MaTho varchar(10) = 'NVMAU' + RIGHT('00000' + CONVERT(varchar(5), (@SoThuTu - 1) % 9 + 1), 5);
    DECLARE @HoTenNhanVien nvarchar(100), @TenChucVu nvarchar(100);
    DECLARE @HoTenKhach nvarchar(100), @TenDichVu nvarchar(150), @TenPhuTung nvarchar(150);
    SELECT @HoTenNhanVien = HoTen, @TenChucVu = TenChucVu FROM @NhanSu WHERE SoThuTu = @SoThuTu;
    SELECT @HoTenKhach = HoTen FROM @Khach WHERE SoThuTu = @SoThuTu;
    SELECT @TenDichVu = TenDichVu, @TenPhuTung = TenPhuTung FROM @DichVuMau WHERE SoThuTu = @SoThuTu;
    DECLARE @DienThoaiNhanVien varchar(15) = '0501' + RIGHT('000000' + CONVERT(varchar(6), @SoThuTu), 6);
    DECLARE @DienThoaiKhach varchar(15) = '0502' + RIGHT('000000' + CONVERT(varchar(6), @SoThuTu), 6);
    DECLARE @DienThoaiNhaCungCap varchar(15) = '0503' + RIGHT('000000' + CONVERT(varchar(6), @SoThuTu), 6);
    DECLARE @BienSo varchar(20) = '29-MD ' + RIGHT('00000' + CONVERT(varchar(5), 71000 + @SoThuTu), 5);
    DECLARE @LoaiXe nvarchar(50) = CASE @SoThuTu % 3 WHEN 0 THEN N'Winner X' WHEN 1 THEN N'Vision' ELSE N'Wave Alpha 110' END;
    DECLARE @NgaySua date = DATEADD(day, -@SoThuTu, @HomNay);
    DECLARE @NgayHoanThanh date = DATEADD(day, 1, @NgaySua);
    DECLARE @GiaNhap decimal(18,0) = 20000 + @SoThuTu * 10000;
    DECLARE @GiaBanCu decimal(18,0) = @GiaNhap + 5000;
    DECLARE @GiaBanMoi decimal(18,0) = @GiaNhap + 10000;
    DECLARE @GiaDichVu decimal(18,0) = 100000 + @SoThuTu * 30000;
    DECLARE @GioBatDau time(0) = CONVERT(time(0), DATEADD(hour, 5 + @SoThuTu, CONVERT(datetime, '19000101')));
    DECLARE @GioKetThuc time(0) = CONVERT(time(0), DATEADD(hour, 6 + @SoThuTu, CONVERT(datetime, '19000101')));

    IF NOT EXISTS (SELECT 1 FROM ChucVu WHERE MaChucVu = @MaChucVu)
        INSERT ChucVu VALUES (@MaChucVu, @TenChucVu, N'Vị trí bổ sung phục vụ vận hành cửa hàng', 1.00 + @SoThuTu * 0.05, 50000);

    IF NOT EXISTS (SELECT 1 FROM KhoanDieuChinh WHERE MaKhoanDieuChinh = @MaKhoan)
        INSERT KhoanDieuChinh VALUES (@MaKhoan, N'Thưởng chuyên cần nhóm ' + CONVERT(nvarchar(2), @SoThuTu), 50000);

    IF NOT EXISTS (SELECT 1 FROM ChucVuDieuChinh WHERE MaChucVu = @MaChucVu AND MaKhoanDieuChinh = @MaKhoan)
        INSERT ChucVuDieuChinh VALUES (@MaChucVu, @MaKhoan);

    IF NOT EXISTS (SELECT 1 FROM NhanVien WHERE MaNhanVien = @MaNhanVien)
        INSERT NhanVien VALUES (@MaNhanVien, @MaChucVu, @HoTenNhanVien, @DienThoaiNhanVien,
            8000000 + @SoThuTu * 300000, 'maumoi' + RIGHT('00' + CONVERT(varchar(2), @SoThuTu), 2), @MatKhau, 1);

    IF NOT EXISTS (SELECT 1 FROM CaLam WHERE MaCaLam = @MaCa)
        INSERT CaLam VALUES (@MaCa, 1, @GioBatDau, @GioKetThuc);

    IF NOT EXISTS (SELECT 1 FROM ChiTietCaLam WHERE MaNhanVien = @MaNhanVien AND MaCaLam = @MaCa)
        INSERT ChiTietCaLam VALUES (@MaNhanVien, @MaCa, DATEADD(day, -1, @HomNay), 1);

    IF NOT EXISTS (SELECT 1 FROM KhachHang WHERE MaKhachHang = @MaKhach)
        INSERT KhachHang VALUES (@MaKhach, @HoTenKhach, @DienThoaiKhach);

    IF NOT EXISTS (SELECT 1 FROM Xe WHERE MaXe = @MaXe)
        INSERT Xe VALUES (@MaXe, @MaKhach, @BienSo, @LoaiXe, N'Honda', N'Đen');

    IF NOT EXISTS (SELECT 1 FROM ChiTietPhanCong WHERE MaNhanVien = @MaTho AND MaXe = @MaXe)
        INSERT ChiTietPhanCong VALUES (@MaTho, @MaXe,
            DATEADD(hour, 9, CONVERT(datetime, @NgaySua)), DATEADD(hour, 11, CONVERT(datetime, @NgaySua)));

    IF NOT EXISTS (SELECT 1 FROM DichVu WHERE MaDichVu = @MaDichVu)
        INSERT DichVu VALUES (@MaDichVu, @TenDichVu, N'Kiểm tra, thay thế phụ tùng và chạy thử trước khi bàn giao.', @GiaDichVu, 1);

    IF NOT EXISTS (SELECT 1 FROM NhaCungCap WHERE MaNhaCungCap = @MaNhaCungCap)
        INSERT NhaCungCap VALUES (@MaNhaCungCap, N'Phụ tùng Minh Anh cơ sở ' + CONVERT(nvarchar(2), @SoThuTu),
            @DienThoaiNhaCungCap, CONVERT(nvarchar(3), 10 + @SoThuTu) + N' Nguyễn Trãi, Hà Nội');

    IF NOT EXISTS (SELECT 1 FROM PhuTung WHERE MaPhuTung = @MaPhuTung)
        INSERT PhuTung VALUES (@MaPhuTung, @MaNhaCungCap, @TenPhuTung, @GiaNhap, @GiaBanMoi, 47, DATEADD(year, 2, @HomNay), 1);

    IF NOT EXISTS (SELECT 1 FROM ChiTietDichVu WHERE MaDichVu = @MaDichVu AND MaPhuTung = @MaPhuTung)
        INSERT ChiTietDichVu VALUES (@MaDichVu, @MaPhuTung, 2);

    IF NOT EXISTS (SELECT 1 FROM PhieuNhap WHERE MaPhieuNhap = @MaPhieuNhap)
        INSERT PhieuNhap VALUES (@MaPhieuNhap, @MaNhanVien, DATEADD(day, -30, @HomNay), N'Nhập phụ tùng bổ sung tháng này', @GiaNhap * 50);

    IF NOT EXISTS (SELECT 1 FROM ChiTietPhieuNhap WHERE MaPhieuNhap = @MaPhieuNhap AND MaPhuTung = @MaPhuTung)
        INSERT ChiTietPhieuNhap VALUES (@MaPhieuNhap, @MaPhuTung, 50, @GiaNhap * 50);

    IF NOT EXISTS (SELECT 1 FROM DieuChinhTonKho WHERE MaDieuChinh = @MaDieuChinh)
        INSERT DieuChinhTonKho (MaDieuChinh, MaPhuTung, MaNhanVien, NgayDieuChinh, SoLuongCu, SoLuongMoi, DonGiaNhap, LyDo)
        VALUES (@MaDieuChinh, @MaPhuTung, @MaNhanVien, DATEADD(day, -20, @HomNay), 50, 49, @GiaNhap, N'Kiểm kê và loại một sản phẩm lỗi');

    IF NOT EXISTS (SELECT 1 FROM DieuChinhGiaBan WHERE MaDieuChinhGia = @MaDieuChinhGia)
        INSERT DieuChinhGiaBan VALUES (@MaDieuChinhGia, @MaPhuTung, @MaNhanVien, DATEADD(day, -19, @HomNay),
            @GiaBanCu, @GiaBanMoi, N'Cập nhật giá bán theo lô mới');

    IF NOT EXISTS (SELECT 1 FROM PhieuTiepNhan WHERE MaPhieuTiepNhan = @MaPhieuSua)
        INSERT PhieuTiepNhan VALUES (@MaPhieuSua, @MaXe, @MaNhanVien, @NgaySua, @NgayHoanThanh, N'Hoàn thành');

    IF NOT EXISTS (SELECT 1 FROM ChiTietPhieuTiepNhan WHERE MaPhieuTiepNhan = @MaPhieuSua AND MaDichVu = @MaDichVu)
        INSERT ChiTietPhieuTiepNhan VALUES (@MaPhieuSua, @MaDichVu, 1, @GiaDichVu);

    IF NOT EXISTS (SELECT 1 FROM HoaDon WHERE MaHoaDon = @MaHoaDon)
        INSERT HoaDon VALUES (@MaHoaDon, @MaPhieuSua, @GiaDichVu, @NgayHoanThanh, @NgayHoanThanh);

    IF NOT EXISTS (SELECT 1 FROM WebDichVu WHERE MaDichVu = @MaDichVu)
        INSERT WebDichVu VALUES (@MaDichVu, N'<p>' + @TenDichVu + N': kiểm tra kỹ thuật, thực hiện dịch vụ và bàn giao xe.</p>',
            '/images/gemini-dv' + RIGHT('00' + CONVERT(varchar(2), @SoThuTu), 2) + '.jpg');

    IF NOT EXISTS (SELECT 1 FROM WebLichHen WHERE MaLichHen = @MaLichHen)
        INSERT WebLichHen (MaLichHen, TenKhachHang, SoDienThoai, BienXe, LoaiXe, HangSanXuat, MauSac,
            MaDichVu, NgayHen, GhiChu, TrangThai, MaPhieuTiepNhan, NgayTao)
        VALUES (@MaLichHen, @HoTenKhach, @DienThoaiKhach, @BienSo, @LoaiXe, N'Honda', N'Đen',
            @MaDichVu, DATEADD(hour, 9, CONVERT(datetime2, @NgaySua)), N'Lịch hẹn mẫu đã hoàn thành sửa chữa',
            N'Đã tiếp nhận', @MaPhieuSua, DATEADD(hour, 8, CONVERT(datetime2, @NgaySua)));

    IF NOT EXISTS (SELECT 1 FROM WebPhieuTiepNhan WHERE MaPhieuTiepNhan = @MaPhieuSua)
        INSERT WebPhieuTiepNhan VALUES (@MaPhieuSua);

    IF NOT EXISTS (SELECT 1 FROM WebPhieuPhuTung WHERE MaPhieuTiepNhan = @MaPhieuSua AND MaPhuTung = @MaPhuTung)
        INSERT WebPhieuPhuTung VALUES (@MaPhieuSua, @MaPhuTung, 2);

    SET @SoThuTu += 1;
END;

COMMIT TRANSACTION;
