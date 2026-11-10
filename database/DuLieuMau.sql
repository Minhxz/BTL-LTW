SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF EXISTS (SELECT 1 FROM KhachHang WHERE MaKhachHang = 'KHM01')
BEGIN
    PRINT N'Dữ liệu mẫu đã có, bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @HomNay date = CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time' AS date);
DECLARE @SoNgayTrongThang int = DAY(@HomNay);

INSERT INTO NhaCungCap (MaNhaCungCap, TenNhaCungCap, SoDienThoai, DiaChi) VALUES
('NCC09', N'Công ty TNHH Phụ tùng Thành Đạt', '02253888101', N'56 Văn Cao, Ngô Quyền, Hải Phòng'),
('NCC10', N'Đại lý Ắc quy Phát Lộc', '02253888102', N'210 Trần Nguyên Hãn, Lê Chân, Hải Phòng'),
('NCC11', N'Cửa hàng Phụ tùng Hưng Thịnh', '02253888103', N'15 Lê Lợi, Ngô Quyền, Hải Phòng'),
('NCC12', N'Công ty CP Dầu nhớt Việt Phát', '02253888104', N'88 Nguyễn Văn Linh, Lê Chân, Hải Phòng'),
('NCC13', N'Đại lý Lốp xe Nam Hải', '02253888105', N'402 Tôn Đức Thắng, An Dương, Hải Phòng'),
('NCC14', N'Công ty TNHH Thiết bị Xe máy Toàn Cầu', '02253888106', N'27 Cầu Đất, Ngô Quyền, Hải Phòng'),
('NCC15', N'Cửa hàng Phụ kiện Minh Châu', '02253888107', N'9 Hai Bà Trưng, Lê Chân, Hải Phòng');

INSERT INTO KhoanDieuChinh (MaKhoanDieuChinh, TenKhoanDieuChinh, SoTien) VALUES
('KDC11', N'Thưởng khách hàng đánh giá tốt', 200000),
('KDC12', N'Thưởng vượt chỉ tiêu xe trong tháng', 500000),
('KDC13', N'Phụ cấp ăn trưa', 600000),
('KDC14', N'Phạt đi muộn quá 3 lần', -150000),
('KDC15', N'Phạt để khách phàn nàn về chất lượng', -300000);

INSERT INTO CaLam (MaCaLam, ThoiGianCaLam, ThoiGianBatDau, ThoiGianKetThuc) VALUES
('CA04', 4.50, '06:30', '11:00'),
('CA05', 4.00, '11:00', '15:00'),
('CA06', 4.00, '15:00', '19:00');

INSERT INTO PhuTung (MaPhuTung, MaNhaCungCap, TenPhuTung, DonGiaNhap, DonGiaBan, SoLuongTon, HanSuDung, TrangThai) VALUES
('PTM01', 'NCC09', N'Phốt giảm xóc trước Honda Wave', 35000, 60000, 3, DATEADD(year, 2, @HomNay), 1),
('PTM02', 'NCC12', N'Nhớt Shell Advance AX7 10W-40 0.8L', 90000, 125000, 12, DATEADD(day, 20, @HomNay), 1),
('PTM03', 'NCC10', N'Ắc quy GS GTZ7V 12V-6Ah', 380000, 480000, 4, DATEADD(year, 1, @HomNay), 1),
('PTM04', 'NCC13', N'Săm xe Casumina 2.50-17', 45000, 70000, 5, DATEADD(year, 3, @HomNay), 1);

DECLARE @Khach TABLE (STT int PRIMARY KEY, Ten nvarchar(100), SDT varchar(15), BienSo varchar(20), MauXe nvarchar(50), Hang nvarchar(50), Mau nvarchar(30));
INSERT INTO @Khach VALUES
(1,  N'Nguyễn Thị Hồng Nhung', '0912500101', '15B1-234.56', N'Vision', N'Honda', N'Trắng'),
(2,  N'Trần Minh Khoa', '0912500102', '15B2-118.20', N'Lead 125', N'Honda', N'Đỏ'),
(3,  N'Lê Quang Vinh', '0912500103', '15K1-552.10', N'Wave Alpha 110', N'Honda', N'Xanh'),
(4,  N'Phạm Thu Trang', '0912500104', '15B3-907.33', N'Air Blade 125', N'Honda', N'Đen'),
(5,  N'Hoàng Đức Anh', '0912500105', '15F1-661.45', N'Exciter 155', N'Yamaha', N'Xanh GP'),
(6,  N'Vũ Thị Mai', '0912500106', '15B4-310.78', N'Janus', N'Yamaha', N'Hồng'),
(7,  N'Đặng Văn Hùng', '0912500107', '15K2-448.09', N'Future 125', N'Honda', N'Nâu'),
(8,  N'Bùi Ngọc Ánh', '0912500108', '15B5-725.61', N'SH Mode 125', N'Honda', N'Xám'),
(9,  N'Đỗ Mạnh Cường', '0912500109', '15F2-093.17', N'Winner X', N'Honda', N'Đỏ đen'),
(10, N'Ngô Thanh Hương', '0912500110', '15B6-582.40', N'Grande', N'Yamaha', N'Trắng'),
(11, N'Dương Văn Thắng', '0912500111', '15K3-217.88', N'Sirius', N'Yamaha', N'Đen'),
(12, N'Lý Thị Hạnh', '0912500112', '15B7-640.05', N'Vision', N'Honda', N'Đỏ'),
(13, N'Phan Quốc Bảo', '0912500113', '15B8-133.92', N'Lead 125', N'Honda', N'Bạc'),
(14, N'Trịnh Văn Long', '0912500114', '15K4-875.26', N'Wave Alpha 110', N'Honda', N'Đỏ'),
(15, N'Mai Phương Thảo', '0912500115', '15B9-402.71', N'Air Blade 125', N'Honda', N'Xanh'),
(16, N'Cao Đức Thịnh', '0912500116', '15F3-726.54', N'Exciter 155', N'Yamaha', N'Đen'),
(17, N'Tạ Thị Lan', '0912500117', '15C1-389.13', N'Vision', N'Honda', N'Xanh ngọc'),
(18, N'Hồ Văn Phúc', '0912500118', '15K5-504.67', N'Future 125', N'Honda', N'Đen'),
(19, N'Kiều Minh Tuấn', '0912500119', '15C2-218.36', N'Janus', N'Yamaha', N'Xanh'),
(20, N'La Thị Ngọc', '0912500120', '15C3-947.82', N'SH Mode 125', N'Honda', N'Trắng'),
(21, N'Quách Văn Tài', '0912500121', '15K6-318.24', N'Sirius', N'Yamaha', N'Xanh đen'),
(22, N'Âu Thị Diệu', '0912500122', '15C4-672.90', N'Vision', N'Honda', N'Đen nhám'),
(23, N'Mạc Đình Khải', '0912500123', '15C5-805.37', N'Lead 125', N'Honda', N'Vàng cát');

INSERT INTO KhachHang (MaKhachHang, TenKhachHang, SoDienThoai)
SELECT 'KHM' + RIGHT('0' + CAST(STT AS varchar), 2), Ten, SDT FROM @Khach;

INSERT INTO Xe (MaXe, MaKhachHang, BienXe, LoaiXe, HangSanXuat, MauSac)
SELECT 'XEM' + RIGHT('0' + CAST(STT AS varchar), 2), 'KHM' + RIGHT('0' + CAST(STT AS varchar), 2), BienSo, MauXe, Hang, Mau FROM @Khach;

DECLARE @Phieu TABLE (
    STT int PRIMARY KEY, NgayLui int, TrangThai nvarchar(20), NguoiNhan varchar(10),
    DichVu1 varchar(10), SoLuong1 int, DichVu2 varchar(10) NULL, Tho varchar(10) NULL, ThuTien bit);
INSERT INTO @Phieu VALUES
(1,  9, N'Hoàn thành', 'NV015', 'DV04', 1, 'DV13', 'NV002', 1),
(2,  9, N'Hoàn thành', 'NV016', 'DV10', 1, NULL,   'NV003', 1),
(3,  8, N'Hoàn thành', 'NV015', 'DV05', 1, NULL,   'NV004', 1),
(4,  7, N'Hoàn thành', 'NV017', 'DV14', 1, 'DV03', 'NV005', 1),
(5,  6, N'Hoàn thành', 'NV016', 'DV17', 1, NULL,   'NV002', 1),
(6,  6, N'Hoàn thành', 'NV015', 'DV09', 1, 'DV20', 'NV006', 1),
(7,  5, N'Hoàn thành', 'NV001', 'DV16', 1, NULL,   'NV003', 1),
(8,  4, N'Hoàn thành', 'NV017', 'DV01', 1, 'DV07', 'NV007', 1),
(9,  3, N'Hoàn thành', 'NV015', 'DV05', 1, 'DV19', 'NV004', 1),
(10, 0, N'Hoàn thành', 'NV016', 'DV04', 1, 'DV19', 'NV008', 1),
(11, 0, N'Hoàn thành', 'NV015', 'DV15', 1, NULL,   'NV009', 1),
(12, 0, N'Hoàn thành', 'NV001', 'DV04', 1, 'DV06', 'NV010', 1),
(13, 2, N'Hoàn thành', 'NV015', 'DV02', 1, 'DV18', 'NV011', 0),
(14, 1, N'Hoàn thành', 'NV016', 'DV12', 1, NULL,   'NV013', 0),
(15, 1, N'Hoàn thành', 'NV017', 'DV10', 1, 'DV08', 'NV014', 0),
(16, 2, N'Đang sửa', 'NV015', 'DV05', 1, NULL,   'NV002', 0),
(17, 1, N'Đang sửa', 'NV016', 'DV04', 1, 'DV13', 'NV003', 0),
(18, 1, N'Đang sửa', 'NV001', 'DV16', 1, NULL,   'NV004', 0),
(19, 0, N'Đang sửa', 'NV015', 'DV17', 1, NULL,   'NV005', 0),
(20, 0, N'Đang sửa', 'NV017', 'DV09', 1, 'DV10', 'NV006', 0),
(21, 0, N'Chờ sửa', 'NV015', 'DV01', 1, NULL,   NULL,    0),
(22, 0, N'Chờ sửa', 'NV016', 'DV03', 1, 'DV07', NULL,    0),
(23, 0, N'Chờ sửa', 'NV001', 'DV06', 1, NULL,   'NV007', 0);

UPDATE @Phieu SET NgayLui = NgayLui % @SoNgayTrongThang;

INSERT INTO PhieuTiepNhan (MaPhieuTiepNhan, MaXe, MaNhanVien, NgayLap, NgayHen, TrangThai)
SELECT 'PTNM' + RIGHT('0' + CAST(STT AS varchar), 2),
       'XEM' + RIGHT('0' + CAST(STT AS varchar), 2),
       NguoiNhan,
       DATEADD(day, -NgayLui, @HomNay),
       DATEADD(day, 2, DATEADD(day, -NgayLui, @HomNay)),
       TrangThai
FROM @Phieu;

INSERT INTO ChiTietPhieuTiepNhan (MaPhieuTiepNhan, MaDichVu, SoLuong, ThanhTien)
SELECT 'PTNM' + RIGHT('0' + CAST(p.STT AS varchar), 2), d.MaDichVu, x.SoLuong, d.ThanhTienDichVu * x.SoLuong
FROM @Phieu p
CROSS APPLY (VALUES (p.DichVu1, p.SoLuong1), (p.DichVu2, 1)) x(MaDichVu, SoLuong)
JOIN DichVu d ON d.MaDichVu = x.MaDichVu;

INSERT INTO ChiTietPhanCong (MaNhanVien, MaXe, ThoiGianBatDau, ThoiGianHoanThanh)
SELECT Tho,
       'XEM' + RIGHT('0' + CAST(STT AS varchar), 2),
       DATEADD(hour, 8, CAST(DATEADD(day, -NgayLui, @HomNay) AS datetime2(0))),
       DATEADD(hour, 17, CAST(DATEADD(day, 1 - NgayLui, @HomNay) AS datetime2(0)))
FROM @Phieu
WHERE Tho IS NOT NULL;

INSERT INTO HoaDon (MaHoaDon, MaPhieuTiepNhan, TienThanhToan, NgayLap, NgayThanhToan)
SELECT 'HDM' + RIGHT('0' + CAST(p.STT AS varchar), 2),
       'PTNM' + RIGHT('0' + CAST(p.STT AS varchar), 2),
       (SELECT SUM(c.ThanhTien) FROM ChiTietPhieuTiepNhan c WHERE c.MaPhieuTiepNhan = 'PTNM' + RIGHT('0' + CAST(p.STT AS varchar), 2)),
       DATEADD(day, -p.NgayLui, @HomNay),
       DATEADD(day, -p.NgayLui, @HomNay)
FROM @Phieu p
WHERE p.ThuTien = 1;

DECLARE @PhuTungXuat TABLE (MaPhieu varchar(10), MaPhuTung varchar(10), SoLuong int, PRIMARY KEY (MaPhieu, MaPhuTung));
INSERT INTO @PhuTungXuat (MaPhieu, MaPhuTung, SoLuong)
SELECT c.MaPhieuTiepNhan, d.MaPhuTung, SUM(d.SoLuong * c.SoLuong)
FROM ChiTietPhieuTiepNhan c
JOIN ChiTietDichVu d ON d.MaDichVu = c.MaDichVu
JOIN PhieuTiepNhan p ON p.MaPhieuTiepNhan = c.MaPhieuTiepNhan
WHERE c.MaPhieuTiepNhan LIKE 'PTNM%' AND p.TrangThai IN (N'Chờ sửa', N'Đang sửa')
GROUP BY c.MaPhieuTiepNhan, d.MaPhuTung;

INSERT INTO WebPhieuTiepNhan (MaPhieuTiepNhan)
SELECT MaPhieuTiepNhan FROM PhieuTiepNhan WHERE MaPhieuTiepNhan LIKE 'PTNM%' AND TrangThai IN (N'Chờ sửa', N'Đang sửa');

INSERT INTO WebPhieuPhuTung (MaPhieuTiepNhan, MaPhuTung, SoLuong)
SELECT MaPhieu, MaPhuTung, SoLuong FROM @PhuTungXuat;

UPDATE pt SET pt.SoLuongTon = pt.SoLuongTon - x.TongXuat
FROM PhuTung pt
JOIN (SELECT MaPhuTung, SUM(SoLuong) AS TongXuat FROM @PhuTungXuat GROUP BY MaPhuTung) x ON x.MaPhuTung = pt.MaPhuTung;

DECLARE @Lich TABLE (STT int PRIMARY KEY, Ten nvarchar(100), SDT varchar(15), BienSo varchar(20), MauXe nvarchar(50), Hang nvarchar(50), DichVu varchar(10), NgaySau int, Gio varchar(5), GhiChu nvarchar(200), TrangThai nvarchar(20));
INSERT INTO @Lich VALUES
(1,  N'Nguyễn Văn Bình', '0987600201', '15D1-120.11', N'Vision', N'Honda', 'DV04', 1, '08:30', N'Xe kêu to khi đề máy', N'Chờ xác nhận'),
(2,  N'Trần Thị Thu', '0987600202', '15D1-221.22', N'Lead 125', N'Honda', 'DV01', 1, '09:00', N'', N'Chờ xác nhận'),
(3,  N'Lê Văn Định', '0987600203', '15D2-332.33', N'Wave Alpha 110', N'Honda', 'DV05', 2, '10:30', N'Bảo dưỡng trước khi đi xa', N'Chờ xác nhận'),
(4,  N'Phạm Ngọc Hà', '0987600204', '15D2-443.44', N'Air Blade 125', N'Honda', 'DV10', 2, '14:00', N'Lốp sau mòn', N'Chờ xác nhận'),
(5,  N'Hoàng Minh Đức', '0987600205', '15D3-554.55', N'Exciter 155', N'Yamaha', 'DV17', 3, '08:00', N'Xích kêu, chùng', N'Chờ xác nhận'),
(6,  N'Vũ Thanh Tâm', '0987600206', '15D3-665.66', N'Janus', N'Yamaha', 'DV14', 4, '15:30', N'', N'Chờ xác nhận'),
(7,  N'Đinh Quang Huy', '0987600207', '15D4-776.77', N'Future 125', N'Honda', 'DV16', 5, '09:30', N'Gọi trước khi đến', N'Chờ xác nhận'),
(8,  N'Bùi Thị Yến', '0987600208', '15D4-887.88', N'SH Mode 125', N'Honda', 'DV06', 6, '11:00', N'Xe ì máy', N'Chờ xác nhận'),
(9,  N'Đỗ Văn Toàn', '0987600209', '15D5-998.99', N'Winner X', N'Honda', 'DV05', 7, '16:00', N'', N'Chờ xác nhận'),
(10, N'Ngô Thị Liên', '0987600210', '15D5-109.10', N'Grande', N'Yamaha', 'DV03', 8, '13:30', N'Thay nhớt định kỳ', N'Chờ xác nhận'),
(11, N'Lương Văn Sơn', '0987600211', '15D6-210.21', N'Sirius', N'Yamaha', 'DV11', 2, '10:00', N'Khách bận đột xuất', N'Đã hủy'),
(12, N'Chu Thị Quyên', '0987600212', '15D6-321.32', N'Vision', N'Honda', 'DV07', 3, '09:00', N'', N'Đã hủy'),
(13, N'Tô Văn Kiên', '0987600213', '15D7-432.43', N'Lead 125', N'Honda', 'DV15', 4, '14:30', N'Đặt nhầm ngày', N'Đã hủy');

INSERT INTO WebLichHen (MaLichHen, TenKhachHang, SoDienThoai, BienXe, LoaiXe, HangSanXuat, MauSac, MaDichVu, NgayHen, GhiChu, TrangThai, NgayTao)
SELECT 'LH-' + RIGHT(CONVERT(varchar(32), HASHBYTES('MD5', 'lich-mau-' + CAST(STT AS varchar)), 2), 16),
       Ten, SDT, BienSo, MauXe, Hang, N'Chưa cập nhật', DichVu,
       CAST(CAST(DATEADD(day, NgaySau, @HomNay) AS varchar(10)) + ' ' + Gio AS datetime2(0)),
       GhiChu, TrangThai,
       DATEADD(hour, -STT, CAST(@HomNay AS datetime2(0)))
FROM @Lich;

INSERT INTO WebLichHen (MaLichHen, TenKhachHang, SoDienThoai, BienXe, LoaiXe, HangSanXuat, MauSac, MaDichVu, NgayHen, GhiChu, TrangThai, MaPhieuTiepNhan, NgayTao)
SELECT 'LH-' + RIGHT(CONVERT(varchar(32), HASHBYTES('MD5', 'lich-mau-nhan-' + p.MaPhieuTiepNhan), 2), 16),
       k.TenKhachHang, k.SoDienThoai, x.BienXe, x.LoaiXe, x.HangSanXuat, x.MauSac,
       (SELECT TOP 1 c.MaDichVu FROM ChiTietPhieuTiepNhan c WHERE c.MaPhieuTiepNhan = p.MaPhieuTiepNhan ORDER BY c.MaDichVu),
       DATEADD(hour, 9, CAST(p.NgayLap AS datetime2(0))),
       N'Đặt lịch qua website', N'Đã tiếp nhận', p.MaPhieuTiepNhan,
       DATEADD(day, -1, CAST(p.NgayLap AS datetime2(0)))
FROM PhieuTiepNhan p
JOIN Xe x ON x.MaXe = p.MaXe
JOIN KhachHang k ON k.MaKhachHang = x.MaKhachHang
WHERE p.MaPhieuTiepNhan IN ('PTNM05', 'PTNM12', 'PTNM16', 'PTNM17', 'PTNM21');

INSERT INTO WebDichVu (MaDichVu, MoTaHtml, Anh)
SELECT d.MaDichVu, x.MoTa, N''
FROM (VALUES
    ('DV01', N'<p>Thay nhớt <strong>Castrol Power1</strong> chính hãng, kiểm tra miễn phí lọc gió và bugi.</p>'),
    ('DV02', N'<p>Nhớt tổng hợp <strong>Motul 3100</strong> cho xe chạy nhiều, máy êm và mát hơn.</p>'),
    ('DV03', N'<p>Thay nhớt hộp số cho xe tay ga, nên làm sau mỗi 2 lần thay nhớt máy.</p>'),
    ('DV04', N'<p>Gói bảo dưỡng xe tay ga:</p><ul><li>Vệ sinh lọc gió, nồi</li><li>Kiểm tra bugi, phanh</li><li>Siết ốc toàn xe</li></ul>'),
    ('DV05', N'<p>Gói bảo dưỡng xe số và côn tay: lọc gió, bugi, chỉnh xích và côn.</p>'),
    ('DV06', N'<p>Làm sạch kim phun và buồng đốt, giúp xe <em>bốc hơn, đỡ hao xăng</em>.</p>'),
    ('DV07', N'<p>Thay má phanh trước chính hãng, kiểm tra đĩa phanh miễn phí.</p>'),
    ('DV08', N'<p>Thay má phanh sau, chỉnh lại độ ăn phanh.</p>'),
    ('DV09', N'<p>Lốp <strong>Michelin City Grip 2</strong> bám đường tốt khi trời mưa.</p>'),
    ('DV10', N'<p>Thay lốp sau xe tay ga, cân chỉnh và kiểm tra áp suất.</p>'),
    ('DV11', N'<p>Lốp IRC cho xe số, bền và tiết kiệm.</p>'),
    ('DV12', N'<p>Thay lốp sau xe số, tặng kiểm tra vành.</p>'),
    ('DV13', N'<p>Bugi NGK/Denso giúp xe dễ nổ máy buổi sáng.</p>'),
    ('DV14', N'<p>Thay dây curoa xe tay ga, kiểm tra bi nồi và chuông nồi.</p>'),
    ('DV15', N'<p>Ắc quy GS/Đồng Nai chính hãng, bảo hành 6 tháng.</p>'),
    ('DV16', N'<p>Bộ nhông xích đĩa DID cho Wave/Future, chạy êm, bền.</p>'),
    ('DV17', N'<p>Bộ nhông xích đĩa Recto cho Exciter/Winner.</p>'),
    ('DV20', N'<p>Rửa xe bằng bọt tuyết, dưỡng bóng nhựa nhám.</p>')
) x(MaDichVu, MoTa)
JOIN DichVu d ON d.MaDichVu = x.MaDichVu
WHERE NOT EXISTS (SELECT 1 FROM WebDichVu w WHERE w.MaDichVu = d.MaDichVu);

DECLARE @Nhap TABLE (MaPhieu varchar(10), NgayLui int, NhanVien varchar(10), GhiChu nvarchar(255), MaPhuTung varchar(10), SoLuong int);
INSERT INTO @Nhap VALUES
('PNM01', 8, 'NV018', N'Nhập nhớt và lọc gió đầu tháng', 'PT001', 10),
('PNM01', 8, 'NV018', N'Nhập nhớt và lọc gió đầu tháng', 'PT003', 8),
('PNM01', 8, 'NV018', N'Nhập nhớt và lọc gió đầu tháng', 'PT005', 6),
('PNM02', 5, 'NV019', N'Bổ sung lốp Michelin', 'PT011', 4),
('PNM02', 5, 'NV019', N'Bổ sung lốp Michelin', 'PT012', 4),
('PNM03', 3, 'NV018', N'Bổ sung bugi, má phanh', 'PT007', 10),
('PNM03', 3, 'NV018', N'Bổ sung bugi, má phanh', 'PT009', 6),
('PNM03', 3, 'NV018', N'Bổ sung bugi, má phanh', 'PT010', 6),
('PNM04', 1, 'NV019', N'Nhập dây curoa và ắc quy', 'PT015', 5),
('PNM04', 1, 'NV019', N'Nhập dây curoa và ắc quy', 'PT017', 5);
UPDATE @Nhap SET NgayLui = NgayLui % @SoNgayTrongThang;

INSERT INTO PhieuNhap (MaPhieuNhap, MaNhanVienNhap, NgayNhap, GhiChu, TongTienNhap)
SELECT DISTINCT MaPhieu, NhanVien, DATEADD(day, -NgayLui, @HomNay), GhiChu, 0 FROM @Nhap;

INSERT INTO ChiTietPhieuNhap (MaPhieuNhap, MaPhuTung, SoLuongNhap, ThanhTien)
SELECT n.MaPhieu, n.MaPhuTung, n.SoLuong, n.SoLuong * pt.DonGiaNhap
FROM @Nhap n JOIN PhuTung pt ON pt.MaPhuTung = n.MaPhuTung;

UPDATE pn SET pn.TongTienNhap = (SELECT SUM(c.ThanhTien) FROM ChiTietPhieuNhap c WHERE c.MaPhieuNhap = pn.MaPhieuNhap)
FROM PhieuNhap pn WHERE pn.MaPhieuNhap LIKE 'PNM%';

UPDATE pt SET pt.SoLuongTon = pt.SoLuongTon + x.TongNhap
FROM PhuTung pt
JOIN (SELECT MaPhuTung, SUM(SoLuong) AS TongNhap FROM @Nhap GROUP BY MaPhuTung) x ON x.MaPhuTung = pt.MaPhuTung;

DECLARE @KiemKe TABLE (STT int PRIMARY KEY, MaPhuTung varchar(10), ChenhLech int, NgayLui int, NhanVien varchar(10), LyDo nvarchar(255));
INSERT INTO @KiemKe VALUES
(1, 'PT002', -1, 7, 'NV018', N'Kiểm kê: 1 chai nhớt bị móp vỏ, rò rỉ'),
(2, 'PT013',  1, 6, 'NV019', N'Kiểm kê: thừa 1 lốp so với sổ sách'),
(3, 'PT016', -2, 5, 'NV018', N'Kiểm kê: 2 dây curoa lỗi sản xuất, trả nhà cung cấp'),
(4, 'PT018', -1, 4, 'NV019', N'Kiểm kê: 1 ắc quy hỏng cell'),
(5, 'PT021',  2, 3, 'NV018', N'Kiểm kê: nhận thêm 2 chai hàng tặng từ nhà cung cấp'),
(6, 'PT026', -1, 2, 'NV019', N'Kiểm kê: 1 bugi vỡ sứ khi lắp thử'),
(7, 'PT028', -2, 1, 'NV018', N'Kiểm kê: 2 chai nước làm mát hết hạn, hủy bỏ'),
(8, 'PT032',  3, 0, 'NV019', N'Kiểm kê: thừa 3 bóng đèn chưa nhập sổ');
UPDATE @KiemKe SET NgayLui = NgayLui % @SoNgayTrongThang;

INSERT INTO DieuChinhTonKho (MaDieuChinh, MaPhuTung, MaNhanVien, NgayDieuChinh, SoLuongCu, SoLuongMoi, DonGiaNhap, LyDo)
SELECT 'DCM' + RIGHT('0' + CAST(k.STT AS varchar), 2), k.MaPhuTung, k.NhanVien, DATEADD(day, -k.NgayLui, @HomNay),
       pt.SoLuongTon, pt.SoLuongTon + k.ChenhLech, pt.DonGiaNhap, k.LyDo
FROM @KiemKe k JOIN PhuTung pt ON pt.MaPhuTung = k.MaPhuTung;

UPDATE pt SET pt.SoLuongTon = pt.SoLuongTon + k.ChenhLech
FROM PhuTung pt JOIN @KiemKe k ON k.MaPhuTung = pt.MaPhuTung;

DECLARE @DoiGia TABLE (STT int PRIMARY KEY, MaPhuTung varchar(10), GiaMoi decimal(18,0), NgayLui int, NhanVien varchar(10), LyDo nvarchar(255));
INSERT INTO @DoiGia VALUES
(1, 'PT001', 130000, 7, 'NV018', N'Nhà cung cấp tăng giá nhớt'),
(2, 'PT003', 150000, 7, 'NV018', N'Nhà cung cấp tăng giá nhớt'),
(3, 'PT011', 640000, 6, 'NV019', N'Khuyến mãi lốp Michelin tháng này'),
(4, 'PT012', 710000, 6, 'NV019', N'Khuyến mãi lốp Michelin tháng này'),
(5, 'PT017', 370000, 4, 'NV018', N'Điều chỉnh theo giá thị trường'),
(6, 'PT019', 375000, 3, 'NV019', N'Nhà cung cấp tăng giá'),
(7, 'PT025', 135000, 2, 'NV018', N'Điều chỉnh theo giá hãng'),
(8, 'PT031', 99000, 1, 'NV019', N'Làm tròn giá bán lẻ');
UPDATE @DoiGia SET NgayLui = NgayLui % @SoNgayTrongThang;

INSERT INTO DieuChinhGiaBan (MaDieuChinhGia, MaPhuTung, MaNhanVien, NgayDieuChinh, GiaBanCu, GiaBanMoi, LyDo)
SELECT 'DGM' + RIGHT('0' + CAST(g.STT AS varchar), 2), g.MaPhuTung, g.NhanVien, DATEADD(day, -g.NgayLui, @HomNay),
       pt.DonGiaBan, g.GiaMoi, g.LyDo
FROM @DoiGia g JOIN PhuTung pt ON pt.MaPhuTung = g.MaPhuTung;

UPDATE pt SET pt.DonGiaBan = g.GiaMoi
FROM PhuTung pt JOIN @DoiGia g ON g.MaPhuTung = pt.MaPhuTung;

COMMIT TRANSACTION;
PRINT N'Đã thêm dữ liệu mẫu.';
