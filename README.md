# Quản lý cửa hàng sửa xe — ASP.NET Core MVC

Trong Visual Studio chọn **File → Open → Project/Solution** và mở **QuanLyCuaHangSuaXe.sln**. Thanh chạy phải hiện **http**, Solution Explorer phải có project **QuanLyCuaHangSuaXe.Web**, rồi bấm **F5**. Nếu thanh chạy hiện **Current Document (Program.cs)** thì bạn đang mở thư mục/file riêng, chưa nạp solution.

Hoặc mở **ChayWeb.cmd**, hay chạy:

```powershell
dotnet run --project QuanLyCuaHangSuaXe.Web.csproj --launch-profile http
```

Website: http://localhost:5240 — Sơ đồ luồng: http://localhost:5240/Home/Flow — Quản lý: http://localhost:5240/Account/Login.

CSDL mặc định là **QuanLySuaXe** trên `(localdb)\MSSQLLocalDB`. Dùng trực tiếp 20 bảng của bài BTL-LTTQ; không sử dụng AutoFixCompact. Có thể đổi máy SQL bằng `ConnectionStrings__QuanLySuaXe`. Trên máy nhóm, dùng tài khoản có sẵn trong bảng NhanVien. Bản dữ liệu mẫu có `namtv` (quản lý), `phongtc` (thợ chính), `hamn` (thu ngân), mật khẩu mẫu `123456`; nếu nhóm đã đổi mật khẩu thì dùng mật khẩu hiện tại.

## Chức năng

- Website khách: xem dịch vụ, đặt lịch, lưu/khôi phục/xóa nháp session, tra cứu mã + SĐT, hủy lịch chưa tiếp nhận.
- Tiếp nhận: khách hàng/xe, nhiều dịch vụ, kiểm tra định mức và trừ kho trong transaction, phân công thợ, cập nhật tiến độ, ngày hẹn trả.
- Dịch vụ: CRUD, định mức phụ tùng, ảnh JPG/PNG/WebP, editor mô tả có lọc HTML.
- Kho: nhà cung cấp, phụ tùng/lô hàng, phiếu nhập nhiều dòng, điều chỉnh tồn và lịch sử đổi giá.
- Nhân sự: tài khoản, chức vụ, khoản điều chỉnh, ca làm, chấm công, công cá nhân, đổi mật khẩu.
- Hóa đơn: chỉ lập cho phiếu hoàn thành, tính từ giá chốt, chống thu trùng, xem/in, thống kê ngày và xuất CSV.

## Cấu trúc mã nguồn

- `Classes/XuLyDuLieu.cs`: truy vấn SQL có tham số và transaction.
- `Classes/LamSachHtml.cs`: lọc nội dung editor.
- `Classes/NghiepVuSuaXe.cs`: nghiệp vụ tiếp nhận, phân công và chuyển trạng thái.
- `Classes/PhanQuyenNguoiDung.cs`: xác định vai trò và mã nhân viên.
- `Controllers/`: mỗi lớp điều khiển có file riêng, đặt tên tiếng Việt theo chức năng.
- `Models/`: các lớp dữ liệu nhập, validation và lỗi; tách riêng khỏi controller.
- `Controllers/DichVuController.cs`, `HoaDonController.cs`, `ChamCongController.cs`: các phần dịch vụ, hóa đơn, công cá nhân được tách riêng để phân việc.
- `Classes/DinhNghiaDanhMuc.cs`, `PhanTrang.cs`: cấu hình danh mục và kiểm tra bộ lọc dùng chung.
- `Views/TrangChu`, `Views/QuanLy`, `Views/TaiKhoan`: giao diện Razor. `Views/Shared` giữ quy ước ASP.NET MVC.
- `wwwroot/js/quan-ly.js`: quản lý bằng AJAX; các mã JSON/URL giữ ổn định để tránh đứt liên kết.
- `database/BoSungChoWeb.sql`: 4 bảng mở rộng cho lịch online, ảnh/editor và bản chốt phụ tùng. Chỉ bổ sung, không xóa 20 bảng gốc.

Không chạy lại script cài mới trên dữ liệu nhóm. `database/QuanLySuaXe.sql` chỉ dành cho CSDL chưa tồn tại; gặp CSDL đã có sẽ bật NOEXEC để bỏ qua phần còn lại. Cần mở cửa sổ truy vấn mới sau đó. Website tự bổ sung các bảng Web khi khởi động.

## Phân công và kiểm tra

Đọc [Phân công 5 người](docs/PHAN_CONG_5_NGUOI.md): ghi rõ từng file, hàm, công việc, bảng SQL, tình huống kiểm tra và thứ tự ghép.

Bài dùng MVC/Razor; phân trang, bộ lọc và AJAX; CRUD danh mục; cookie đăng nhập và session nháp; validation/annotation; API GET/POST/PUT/DELETE; upload ảnh; editor mô tả. Các ca kiểm tra thủ công nằm ngay dưới phần việc của từng người. Đã bỏ thư mục tests và scripts theo yêu cầu nhóm.

Kiểm tra bản sau khi sắp xếp mã: build 0 lỗi/0 cảnh báo; JavaScript qua kiểm tra cú pháp; 24 yêu cầu HTTP đạt trên CSDL riêng, gồm các controller mới, nhập kho, tiếp nhận/trừ kho, hủy/hoàn kho, editor và chặn lập hóa đơn sai trạng thái. Chưa kiểm tra trực quan toàn bộ màn hình. Các lần ghép sau cần kiểm tra lại theo phân công; không dùng số liệu thật để demo thay đổi dữ liệu.

## Quy ước nghiệp vụ

Giá dịch vụ là giá trọn gói, giống bài BTL-LTTQ. Trừ phụ tùng khi tiếp nhận, không trừ khi đặt lịch online. Hủy phiếu Chờ sửa do web tạo sẽ hoàn theo bản chốt phụ tùng; phiếu tạo từ ứng dụng khác cần hủy trong ứng dụng gốc. Thợ chính nhận tối đa 3 xe, thợ phụ 2 xe. Thu–nhập kho là chênh lệch dòng tiền, không phải lợi nhuận. Mật khẩu SHA-256 giữ tương thích ứng dụng BTL-LTTQ; muốn đổi thuật toán cần cập nhật đồng thời cả hai ứng dụng.

Mã bản 5 bảng cũ đã được lưu ngoài project tại `../BTL-LTW/BanLuuTru/MaCuTruocKhiVietHoa.zip`. Thư mục dự án hiện tại là `C:\Users\buiqu\Desktop\BTL-LTW1`; mở `QuanLyCuaHangSuaXe.sln` ngay trong thư mục này. Remote Git: `https://github.com/Minhxz/BTL-LTW.git`. Các thay đổi hiện tại chưa được commit/push.
