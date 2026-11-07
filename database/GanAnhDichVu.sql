-- Run on QuanLySuaXe after copying the Gemini images to wwwroot/images.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'QuanLySuaXe'
    THROW 50001, N'Chon co so du lieu QuanLySuaXe.', 1;

DECLARE @AnhDichVu table (MaDichVu varchar(10) PRIMARY KEY, Anh nvarchar(250) NOT NULL);
INSERT INTO @AnhDichVu (MaDichVu, Anh) VALUES
    ('DV01', N'/images/gemini-dv01.jpg'), ('DV02', N'/images/gemini-dv02.jpg'),
    ('DV03', N'/images/gemini-dv03.jpg'), ('DV04', N'/images/gemini-dv05.jpg'),
    ('DV05', N'/images/bao-duong-xe-so-con-tay.png'), ('DV06', N'/images/gemini-dv06.jpg'),
    ('DV07', N'/images/gemini-dv07.jpg'), ('DV08', N'/images/gemini-dv08.jpg'),
    ('DV09', N'/images/gemini-dv09.jpg'), ('DV10', N'/images/gemini-dv10.jpg'),
    ('DV11', N'/images/gemini-dv11.jpg'), ('DV12', N'/images/gemini-dv12.jpg'),
    ('DV13', N'/images/gemini-dv13.jpg'), ('DV14', N'/images/gemini-dv14.jpg'),
    ('DV15', N'/images/gemini-dv15.jpg'), ('DV16', N'/images/gemini-dv16.jpg'),
    ('DV17', N'/images/gemini-dv17.jpg'), ('DV18', N'/images/gemini-dv18.jpg'),
    ('DV19', N'/images/gemini-dv19.jpg'), ('DV20', N'/images/gemini-dv20.jpg'),
    ('DV21', N'/images/gemini-dv21.jpg'), ('DV22', N'/images/gemini-dv22.jpg');

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM @AnhDichVu a LEFT JOIN dbo.DichVu d ON d.MaDichVu = a.MaDichVu WHERE d.MaDichVu IS NULL)
    THROW 50002, N'Khong tim thay du ma dich vu DV01-DV22.', 1;

UPDATE w SET Anh = a.Anh
FROM dbo.WebDichVu w WITH (UPDLOCK, HOLDLOCK)
JOIN @AnhDichVu a ON a.MaDichVu = w.MaDichVu
WHERE w.Anh <> a.Anh;

INSERT INTO dbo.WebDichVu (MaDichVu, Anh)
SELECT a.MaDichVu, a.Anh FROM @AnhDichVu a
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.WebDichVu w WITH (UPDLOCK, HOLDLOCK) WHERE w.MaDichVu = a.MaDichVu
);

IF (SELECT COUNT(*) FROM dbo.WebDichVu w JOIN @AnhDichVu a ON a.MaDichVu = w.MaDichVu AND a.Anh = w.Anh) <> 22
    THROW 50003, N'Gan anh chua du 22 dich vu.', 1;

COMMIT TRANSACTION;
SELECT w.MaDichVu, w.Anh FROM dbo.WebDichVu w JOIN @AnhDichVu a ON a.MaDichVu = w.MaDichVu ORDER BY w.MaDichVu;
