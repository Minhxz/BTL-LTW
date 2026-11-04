# Phân công 5 người — Quản lý cửa hàng sửa xe

Thay Người 1–5 bằng họ tên/MSSV. Đây là phân công phụ trách mã hiện có: mỗi người đọc, sửa, kiểm tra và thuyết trình phần mình. Đường dẫn bên dưới tính từ thư mục BTL-LTW1; mở file rồi Ctrl+F tên hàm để đến đúng phần.

## Tổng quan

| Người | Phần phụ trách | Màn hình để demo |
|---|---|---|
| 1 | Tài khoản, phân quyền, nhân sự, chấm công | /Account/Login; quản lý → Nhân sự; Tài khoản cá nhân |
| 2 | Trang khách, khách hàng/xe, đặt lịch và tra cứu | /; /Home/Booking; /Home/Tracking; quản lý → Lịch hẹn |
| 3 | Dịch vụ, ảnh/editor, tiếp nhận và sửa chữa | Quản lý → Dịch vụ, Định mức phụ tùng, Tiếp nhận & sửa chữa |
| 4 | Nhà cung cấp, phụ tùng, nhập kho, kiểm kê | Quản lý → Kho & nhà cung cấp |
| 5 | Hóa đơn, tổng quan, báo cáo, ghép bài | Quản lý → Hóa đơn, Báo cáo doanh thu; /Home/Flow |

## Người 1 — Tài khoản, nhân sự và chấm công

| Vào file | Tìm hàm/phần | Làm gì |
|---|---|---|
| Controllers/XacThucController.cs | DangNhap, DangXuat, ThongTinPhien, DoiMatKhau, LayMaChongGiaMao | Xử lý đăng nhập, cookie, đổi mật khẩu và mã chống giả mạo yêu cầu |
| Controllers/TaiKhoanController.cs | DangNhap, TuChoiTruyCap | Trả trang đăng nhập và trang từ chối truy cập |
| Classes/PhanQuyenNguoiDung.cs | XacDinhVaiTro, MaNhanVien | Ánh xạ chức vụ SQL sang quyền; lấy nhân viên đang đăng nhập |
| Controllers/ChamCongController.cs | CongCaNhan | Lọc công theo nhân viên đăng nhập và tính tổng giờ |
| Controllers/DanhMucController.cs | KiemTraNhanVien, KiemTraChucVu, KiemTraCaLam, KiemTraChamCong | Kiểm tra tài khoản, ca hợp lệ, ca trùng giờ và số giờ thực tế |
| Classes/DinhNghiaDanhMuc.cs | employees, roles, adjustments, roleAdjustments, shifts, attendance | Khai báo trường nhập, khóa, quyền của 6 danh mục nhân sự |
| Models/ThongTinDangNhap.cs; Models/YeuCauDoiMatKhau.cs | Các thuộc tính và annotation | Giới hạn tên đăng nhập, mật khẩu, dữ liệu bắt buộc |
| Views/TaiKhoan/DangNhap.cshtml; Views/TaiKhoan/TuChoiTruyCap.cshtml | Form và thông báo | Giao diện đăng nhập, hiện/ẩn mật khẩu, báo lỗi |
| wwwroot/js/dang-nhap.js; wwwroot/css/dang-nhap.css | Submit form; bố cục | Gửi đăng nhập, xử lý kết quả và giao diện |
| wwwroot/js/quan-ly.js | taiDuLieu: profile/myAttendance; xuLyThaoTac: password | Màn công cá nhân và hộp thoại đổi mật khẩu |
| Program.cs | AddAuthentication, AddSession, AddAntiforgery, OnValidatePrincipal | Cấu hình cookie/session và kiểm tra tài khoản còn hoạt động |

**Bảng SQL:** NhanVien, ChucVu, KhoanDieuChinh, ChucVuDieuChinh, CaLam, ChiTietCaLam.

**Tự kiểm tra:** đăng nhập đúng/sai; mỗi vai trò chỉ thấy quyền của mình; nhân viên nghỉ không đăng nhập; không tự khóa tài khoản; không chấm công tương lai/trùng giờ/vượt thời lượng ca. Đổi mật khẩu bằng tài khoản demo riêng, tránh đổi tài khoản chung của nhóm.

**Bàn giao:** tài khoản demo 3 vai trò, danh sách quyền, ảnh đăng nhập và chấm công. Demo: đăng nhập → tạo nhân viên → tạo ca → chấm công → xem công cá nhân.

## Người 2 — Khách hàng, xe và lịch hẹn

| Vào file | Tìm hàm/phần | Làm gì |
|---|---|---|
| Controllers/TrangChuController.cs | TrangChu, DatLich, TraCuu | Điều hướng và trả giao diện khách |
| Controllers/LichHenController.cs | DatLich, ChuanHoaDienThoai, TraCuu, HuyLich | Kiểm tra giờ/SĐT, lưu lịch, tra cứu bằng mã + SĐT, hủy lịch chờ |
| Controllers/LichHenController.cs | DanhSachLichHen, TiepNhanLich, TuChoiLich | Nhân viên xem/lọc lịch, nhận hoặc từ chối; nhận lịch gọi nghiệp vụ Người 3 |
| Controllers/BanNhapController.cs | DocBanNhap, LuuBanNhap, XoaBanNhap | Lưu/đọc/xóa nháp theo session trình duyệt |
| Models/YeuCauDatLich.cs; Models/YeuCauHuyLich.cs; Models/YeuCauNhanLich.cs; Models/BanNhapDatLich.cs | Thuộc tính, annotation | Dữ liệu gửi lên cho lịch, hủy, nhận lịch và nháp |
| Classes/DinhNghiaDanhMuc.cs | customers, vehicles | Trường nhập khách hàng, xe; liên kết khóa khách |
| Controllers/DanhMucController.cs | Luu: vehicles | Chặn sửa xe đã có phiếu để giữ lịch sử |
| Views/TrangChu/TrangChu.cshtml; DatLich.cshtml; TraCuu.cshtml; _DatLich.cshtml | Trang khách và form | Form đặt lịch, mã tra cứu, kết quả tiến độ |
| Views/TrangChu/_GioiThieu.cshtml; _QuyTrinh.cshtml; QuyenRiengTu.cshtml | Nội dung trang chủ | Giới thiệu, quy trình, thông tin website |
| Views/Shared/_Layout.cshtml; _DauTrang.cshtml; _ChanTrang.cshtml | Khung dùng chung | Menu, đầu/cuối trang khách, nhúng CSS/JS |
| wwwroot/js/dat-lich.js; tra-cuu.js; trang-chu.js | Form, nháp, tra cứu, menu | Gọi API, hiển thị lỗi, sao chép mã và điều hướng |
| wwwroot/css/trang-chu.css; danh-muc.css | Trang khách | Bố cục máy tính/điện thoại và form |
| wwwroot/js/quan-ly.js | taiDuLieu: bookings; nhanLich; xuLyThaoTac: accept/reject | Danh sách lịch và thao tác tiếp nhận/hủy của nhân viên |

**Bảng SQL:** KhachHang, Xe, WebLichHen. Không tự viết lại phần trừ kho: gọi NghiepVuSuaXe.TaoPhieu của Người 3.

**Tự kiểm tra:** đặt lịch hợp lệ có mã; sai giờ/SĐT bị chặn; sai cặp mã/SĐT không xem được lịch; nháp riêng theo session; không hủy lịch đã tiếp nhận; nhận lịch hai lần bị chặn.

**Bàn giao:** ảnh form, mã lịch demo, các tình huống đúng/sai. Demo: lưu nháp → đặt lịch → tra cứu → nhân viên nhận lịch → xem phiếu liên kết.

## Người 3 — Dịch vụ và sửa chữa

| Vào file | Tìm hàm/phần | Làm gì |
|---|---|---|
| Controllers/DichVuController.cs | DanhSachDichVu, DocNoiDungDichVu, LuuNoiDungDichVu, TaiAnhLen | API dịch vụ công khai; nội dung editor; tải JPG/PNG/WebP |
| Models/NoiDungDichVu.cs; Classes/LamSachHtml.cs | LocNoiDung | Giới hạn mô tả và lọc thẻ/thuộc tính không cho phép |
| Controllers/TiepNhanController.cs | DanhSach, ChiTiet, TaoPhieu, DoiTrangThai, DoiNgayHen, PhanCong, BoPhanCong | API danh sách/chi tiết phiếu, tiến độ, ngày trả và thợ |
| Classes/NghiepVuSuaXe.cs | TaoPhieuAsync, TaoPhieu | Điều phối tiếp nhận trong cùng một giao dịch SQL |
| Classes/NghiepVuSuaXe.cs | KiemTraXe, TinhDichVu, TruKho, LayKhachHang | Chặn xe đang sửa; tính giá/định mức; trừ kho; tìm hoặc tạo khách |
| Classes/NghiepVuSuaXe.cs | PhanCong, DoiTrangThaiAsync | Giới hạn số xe của thợ; thứ tự trạng thái; hủy hoàn kho |
| Models/ThongTinSuaChua.cs | DichVuChon, ThongTinTiepNhan, YeuCauTrangThai, YeuCauPhanCong, YeuCauNgayHen | Cấu trúc thông tin tiếp nhận, dịch vụ, phân công và ngày hẹn |
| Classes/DinhNghiaDanhMuc.cs; Controllers/DanhMucController.cs | services, serviceParts; KiemTraDinhMuc | CRUD dịch vụ/định mức; chặn đổi định mức khi đang có xe xử lý |
| Views/TrangChu/_DichVu.cshtml; wwwroot/js/dich-vu.js | Danh sách/chi tiết dịch vụ công khai | Hiển thị giá, mô tả, ảnh dịch vụ |
| wwwroot/js/quan-ly.js | tiepNhanXe, themDong, xemPhieu, suaNoiDungDichVu, locNoiDung | Form nhiều dịch vụ, chi tiết phiếu, editor và ảnh |
| wwwroot/js/quan-ly.js | xuLyThaoTac: status/assign/unassign/due-date/content/bold/italic/list | Gắn nút thao tác với API tương ứng |

**Bảng SQL:** DichVu, ChiTietDichVu, PhieuTiepNhan, ChiTietPhieuTiepNhan, ChiTietPhanCong, WebDichVu, WebPhieuTiepNhan, WebPhieuPhuTung. Đọc KhachHang, Xe và PhuTung khi tiếp nhận.

**Tự kiểm tra:** nhiều dịch vụ tính đúng giá; thiếu kho thì không lưu một phần; hủy phiếu chờ hoàn đúng kho một lần; không hủy khi đã sửa; thợ chỉ sửa phiếu được giao; ảnh giả/ảnh quá lớn bị chặn; mô tả giữ đậm/nghiêng nhưng loại script.

**Bàn giao:** dịch vụ có định mức và ảnh, phiếu demo đủ trạng thái. Demo: dịch vụ → ảnh/editor → tiếp nhận → phân công → đang sửa → hoàn thành; chuyển Người 5 lập hóa đơn.

## Người 4 — Kho và nhà cung cấp

| Vào file | Tìm hàm/phần | Làm gì |
|---|---|---|
| Controllers/KhoController.cs | DanhSach, ChiTiet, NhapKho | Lọc phiếu nhập, xem dòng nhập, nhập nhiều phụ tùng và cộng tồn |
| Controllers/KhoController.cs | DieuChinhTon, LichSuDieuChinh | Kiểm kê, ghi lý do, xem lịch sử tồn và giá |
| Models/PhuTungNhap.cs; ThongTinPhieuNhap.cs; YeuCauDieuChinhTon.cs | Thuộc tính và annotation | Mã phụ tùng, số lượng, ngày nhập, tồn thực tế, lý do |
| Classes/DinhNghiaDanhMuc.cs | suppliers, parts | Trường nhà cung cấp, lô phụ tùng, giá và hạn dùng |
| Controllers/DanhMucController.cs | KiemTraPhuTung | Tồn ban đầu bằng 0; bảo vệ thông tin lô đã dùng; ghi lịch sử giá bán |
| wwwroot/js/quan-ly.js | nhapKho, themDong | Form phiếu nhập nhiều dòng |
| wwwroot/js/quan-ly.js | taiDuLieu: purchases/stockHistory; xuLyThaoTac: purchase-detail/stock-adjust | Xem phiếu nhập, kiểm kê và lịch sử |

**Bảng SQL:** NhaCungCap, PhuTung, PhieuNhap, ChiTietPhieuNhap, DieuChinhTonKho, DieuChinhGiaBan.

**Tự kiểm tra:** nhập 10 tăng đúng 10; tiền nhập theo giá nhập và số lượng; không nhận phụ tùng hết hạn; kiểm kê bắt buộc lý do; tồn không âm; không đổi nhà cung cấp/hạn dùng/giá nhập của lô đã sử dụng.

**Bàn giao:** nhà cung cấp và phụ tùng đủ tồn cho Người 3 demo, phiếu nhập và lịch sử điều chỉnh. Demo: tạo NCC → tạo phụ tùng tồn 0 → nhập kho → kiểm kê → xem lịch sử.

## Người 5 — Hóa đơn, báo cáo và ghép bài

| Vào file | Tìm hàm/phần | Làm gì |
|---|---|---|
| Controllers/HoaDonController.cs | LapHoaDon, DanhSachHoaDon | Chỉ thu phiếu hoàn thành; chống hóa đơn trùng; tính tiền từ giá chốt; lọc danh sách |
| Models/ThongTinSuaChua.cs | YeuCauThanhToan | Dữ liệu ngày thanh toán |
| Controllers/BaoCaoController.cs | TongQuan, BaoCao, XuatCsv | Số liệu dashboard, báo cáo theo ngày, xuất CSV |
| Controllers/QuanLyController.cs | BangDieuKhien, LuongNghiepVu | Trả giao diện quản lý và luồng nghiệp vụ |
| Views/QuanLy/BangDieuKhien.cshtml; wwwroot/css/quan-ly.css | Menu, vùng nội dung, hộp thoại, in | Khung dùng chung của trang quản lý; phối hợp 4 người còn lại |
| wwwroot/js/quan-ly.js | hienTongQuan, hienBaoCao; taiDuLieu: invoices | Dashboard, biểu đồ/bảng doanh thu và danh sách hóa đơn |
| wwwroot/js/quan-ly.js | xemPhieu; xuLyThaoTac: payment/print-invoice/print | Thu tiền, xem và in hóa đơn; phối hợp phần chi tiết phiếu Người 3 |
| Views/QuanLy/LuongNghiepVu.cshtml | Sơ đồ và bảng phân công | Đồng bộ luồng demo và file phụ trách với tài liệu này |
| appsettings.json; Properties/launchSettings.json; ChayWeb.cmd; README.md | SQL, profile http, cách chạy | Cấu hình máy nhóm, hướng dẫn mở solution, ghép và kiểm tra bài |
| database/QuanLySuaXe.sql; database/BoSungChoWeb.sql | Cấu trúc SQL | Giữ đúng bảng nhóm và các bảng mở rộng; phối hợp chủ chức năng trước khi đổi |

**Bảng SQL:** HoaDon; đọc phiếu tiếp nhận, chi tiết dịch vụ, phiếu nhập và các bảng liên quan cho báo cáo.

**Tự kiểm tra:** chưa hoàn thành không thu; mỗi phiếu chỉ có một hóa đơn; tổng từ chi tiết đã chốt; ngày lọc báo cáo/CSV thống nhất; thu ngân không vào báo cáo chỉ dành cho quản lý; in hóa đơn dễ đọc.

**Bàn giao:** bản ghép chạy F5, ảnh báo cáo/in hóa đơn, kết quả kiểm tra từng người. Demo: thu tiền → chặn thu trùng → lọc doanh thu → xuất CSV → in hóa đơn.

## File dùng chung: ai sửa phần nào?

| File | Chủ trì | Quy tắc sửa |
|---|---|---|
| Classes/XuLyDuLieu.cs | Người 3 | Kết nối, truy vấn có tham số, giao dịch; thay đổi ảnh hưởng cả 5 phần |
| Classes/DinhNghiaDanhMuc.cs | Người 1 tổng hợp | Người 1: nhân sự; 2: customers/vehicles; 3: services/serviceParts; 4: suppliers/parts |
| Controllers/DanhMucController.cs | Người 1 tổng hợp | Sửa hàm kiểm tra thuộc phần mình; Them/Sua/Xoa/Luu là bộ CRUD dùng chung |
| Classes/PhanTrang.cs | Người 5 | Kiểm tra phân trang/khoảng ngày dùng chung |
| wwwroot/js/quan-ly.js | Người 5 ghép | Mỗi người sửa đúng hàm/nhánh đã ghi trên; không thay cả file bằng bản cũ |
| wwwroot/js/goi-api.js | Người 1 | Gửi fetch, CSRF và xử lý lỗi chung |
| Program.cs | Người 1; Người 5 ghép | Thống nhất cookie, DI, đường dẫn trước khi ghép |
| Views/Shared; CSS dùng chung | Người 2 và 5 | Người 2 chủ trì trang khách; Người 5 chủ trì trang quản lý |

Tên lớp/hàm nghiệp vụ dùng tiếng Việt không dấu. Tên quy ước ASP.NET, khóa JSON và địa chỉ API giữ ổn định để giao diện vẫn gọi đúng. Không đổi riêng một đầu của API. Không đổi tên bảng/cột SQL đang dùng chung với BTL-LTTQ.

## Thứ tự ghép và luồng bảo vệ

1. Người 1 kiểm tra đăng nhập/phân quyền; Người 5 xác nhận cả nhóm chạy cùng cấu trúc SQL.
2. Người 4 chuẩn bị nhà cung cấp, phụ tùng và tồn kho; Người 3 tạo dịch vụ/định mức.
3. Người 2 tạo lịch; Người 3 nhận xe, phân công, cập nhật hoàn thành.
4. Người 5 lập hóa đơn và đối chiếu báo cáo; cả nhóm thử các trường hợp bị chặn.
5. Mỗi người nộp phần file đã sửa, ảnh demo và kết quả đúng/sai; Người 5 build bản ghép cuối. Không còn thư mục test/script trong bài; kiểm tra theo các ca ở từng mục trên.

Luồng chung: Nhà cung cấp → Phụ tùng → Nhập kho → Dịch vụ/định mức → Đặt lịch → Tiếp nhận/trừ kho → Phân công → Đang sửa → Hoàn thành → Hóa đơn → Báo cáo.
