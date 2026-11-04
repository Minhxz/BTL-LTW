-- Additive, repeatable web metadata only. Run against the EXISTING QuanLySuaXe.
-- The 20 business tables and their data are never recreated by the web application.
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.WebDichVu', N'U') IS NULL
CREATE TABLE dbo.WebDichVu (
    MaDichVu varchar(10) NOT NULL PRIMARY KEY REFERENCES dbo.DichVu(MaDichVu),
    MoTaHtml nvarchar(max) NOT NULL DEFAULT N'',
    Anh nvarchar(250) NOT NULL DEFAULT N''
);
IF OBJECT_ID(N'dbo.WebLichHen', N'U') IS NULL
CREATE TABLE dbo.WebLichHen (
    MaLichHen varchar(24) NOT NULL PRIMARY KEY,
    TenKhachHang nvarchar(100) NOT NULL,
    SoDienThoai varchar(15) NOT NULL,
    BienXe varchar(20) NOT NULL,
    LoaiXe nvarchar(50) NOT NULL,
    HangSanXuat nvarchar(50) NOT NULL,
    MauSac nvarchar(30) NOT NULL,
    MaDichVu varchar(10) NOT NULL REFERENCES dbo.DichVu(MaDichVu),
    NgayHen datetime2(0) NOT NULL,
    GhiChu nvarchar(1000) NOT NULL,
    TrangThai nvarchar(20) NOT NULL DEFAULT N'Chờ xác nhận',
    MaPhieuTiepNhan varchar(10) NULL REFERENCES dbo.PhieuTiepNhan(MaPhieuTiepNhan),
    NgayTao datetime2(0) NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT CK_WebLichHen_TrangThai CHECK (TrangThai IN(N'Chờ xác nhận',N'Đã tiếp nhận',N'Đã hủy'))
);
IF OBJECT_ID(N'dbo.WebPhieuPhuTung', N'U') IS NULL
CREATE TABLE dbo.WebPhieuPhuTung (
    MaPhieuTiepNhan varchar(10) NOT NULL REFERENCES dbo.PhieuTiepNhan(MaPhieuTiepNhan),
    MaPhuTung varchar(10) NOT NULL REFERENCES dbo.PhuTung(MaPhuTung),
    SoLuong int NOT NULL CHECK (SoLuong>0),
    PRIMARY KEY(MaPhieuTiepNhan, MaPhuTung)
);

IF OBJECT_ID(N'dbo.WebPhieuTiepNhan',N'U') IS NULL CREATE TABLE dbo.WebPhieuTiepNhan(MaPhieuTiepNhan varchar(10) NOT NULL PRIMARY KEY REFERENCES dbo.PhieuTiepNhan(MaPhieuTiepNhan));
