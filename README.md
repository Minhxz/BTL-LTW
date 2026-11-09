# Quản lý cửa hàng sửa xe — ASP.NET Core MVC

Trong Visual Studio chọn **File → Open → Project/Solution** và mở **QuanLyCuaHangSuaXe.sln**. Thanh chạy phải hiện **http**, Solution Explorer phải có project **QuanLyCuaHangSuaXe.Web**, rồi bấm **F5**. Nếu thanh chạy hiện **Current Document (Program.cs)** thì bạn đang mở thư mục/file riêng, chưa nạp solution.

Hoặc mở **ChayWeb.cmd**, hay chạy:

```powershell
dotnet run --project QuanLyCuaHangSuaXe.Web.csproj --launch-profile http
```

Website: http://localhost:5240 — Quản lý: http://localhost:5240/Account/Login.

CSDL mặc định là **QuanLySuaXe** trên `(localdb)\MSSQLLocalDB`. Dùng trực tiếp 20 bảng của bài BTL-LTTQ; không sử dụng AutoFixCompact. Có thể đổi máy SQL bằng `ConnectionStrings__QuanLySuaXe`. Trên máy nhóm, dùng tài khoản có sẵn trong bảng NhanVien. Bản dữ liệu mẫu có `namtv` (quản lý), `phongtc` (thợ chính), `hamn` (thu ngân), mật khẩu mẫu `123456`; nếu nhóm đã đổi mật khẩu thì dùng mật khẩu hiện tại.

Ảnh mẫu của các dịch vụ có sẵn trong `wwwroot/images`. Web tự chọn ảnh theo mã dịch vụ khi chưa có ảnh riêng hoặc tệp ảnh đã lưu không có trên máy. Thành viên chỉ cần pull đủ các tệp ảnh trong repo. Ảnh tải từ trang quản lý nằm trong `wwwroot/uploads/services` và được ưu tiên khi có trên máy.

## Chức năng

- Website khách: xem dịch vụ, đặt lịch, lưu/khôi phục/xóa nháp session, tra cứu mã + SĐT, hủy lịch chưa tiếp nhận.
- Tiếp nhận: khách hàng/xe, nhiều dịch vụ, kiểm tra định mức và trừ kho trong transaction, phân công thợ, cập nhật tiến độ, ngày hẹn trả.
- Dịch vụ: CRUD, định mức phụ tùng, ảnh JPG/PNG/WebP, editor mô tả có lọc HTML.
- Kho: nhà cung cấp, phụ tùng/lô hàng, phiếu nhập nhiều dòng, điều chỉnh tồn và lịch sử đổi giá.
- Nhân sự: 3 mục Nhân viên & chức vụ, Ca làm & chấm công, Thưởng / phạt; các chức năng chi tiết nằm trong từng mục.
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

Kiểm tra ngày 09/11/2026 trên CSDL riêng: bản dựng 0 lỗi/0 cảnh báo; cả 7 file JavaScript hợp lệ; 191 yêu cầu HTTP đạt. Bao gồm thêm/sửa/xóa danh mục, mã tự động và tạo đồng thời, phân quyền, ca làm/chấm công, nhập kho/kiểm kê/lịch sử giá, tiếp nhận/trừ kho/hủy/hoàn kho, phân công/tiến độ, hóa đơn/chống thu trùng, báo cáo/CSV, đặt lịch/tra cứu/hủy/tiếp nhận, nháp theo phiên, đổi mật khẩu, tải ảnh và lọc HTML.

Đã chạy thử trên trình duyệt: 20 mục quản lý, các biểu mẫu thêm mới và ô mã tự động, thêm/lọc nhà cung cấp, danh sách/chi tiết dịch vụ, lưu/khôi phục/xóa nháp, tra cứu, đăng nhập và menu điện thoại. Không ghi nhận lỗi JavaScript trong lượt chạy này.

## Quy ước mã nguồn và mã bản ghi

Tên biến và hàm của dự án dùng tiếng Việt không dấu theo cách đặt tên của BTL-LTTQ. Mỗi mức thụt lề dùng 4 dấu cách; cấu hình trong `.editorconfig`. Các tên bắt buộc của ASP.NET, trình duyệt và thư viện giữ theo hệ thống.

Mã khách hàng, xe, dịch vụ, nhà cung cấp, phụ tùng, nhân viên, chức vụ, khoản điều chỉnh và ca làm hiện ngay khi mở biểu mẫu thêm mới, ví dụ `NV00000001`, `NCC0000001`; ô mã chỉ hiển thị, không nhập tay. Máy chủ dành mã riêng cho từng biểu mẫu và giữ đúng mã khi lưu. Đóng biểu mẫu chưa lưu có thể để trống một số thứ tự. Phiếu tiếp nhận, phiếu nhập, hóa đơn, lịch hẹn và lịch sử điều chỉnh cũng được tự sinh mã. Các liên kết giữa bảng được chọn từ danh sách có sẵn.

`database/DuLieuMauBoSung.sql` bổ sung 12 bản ghi vào từng bảng trong 24 bảng nghiệp vụ và bảng web, tổng cộng 288 bản ghi. Các bản ghi dùng mã có phần `MAU` để nhận biết. Chạy trên CSDL QuanLySuaXe đã có các bảng mở rộng của web; chạy lại không thêm trùng, không xóa hay thay thế dữ liệu hiện có. Tài khoản mẫu bổ sung là `maumoi01` đến `maumoi12`, mật khẩu `123456`.

Đã kiểm tra thêm 91 yêu cầu cho mã hiện sẵn: cả 9 danh mục có mã riêng, mã giữa các biểu mẫu không trùng, mã lưu trùng với mã hiển thị, chặn vé cấp mã sai hoặc dùng nhầm tài khoản, và 8 lần lưu đồng thời.

## Quy ước nghiệp vụ

Giá dịch vụ là giá trọn gói, giống bài BTL-LTTQ. Trừ phụ tùng khi tiếp nhận, không trừ khi đặt lịch online. Hủy phiếu Chờ sửa do web tạo sẽ hoàn theo bản chốt phụ tùng; phiếu tạo từ ứng dụng khác cần hủy trong ứng dụng gốc. Thợ chính nhận tối đa 3 xe, thợ phụ 2 xe. Thu–nhập kho là chênh lệch dòng tiền, không phải lợi nhuận. Mật khẩu SHA-256 giữ tương thích ứng dụng BTL-LTTQ; muốn đổi thuật toán cần cập nhật đồng thời cả hai ứng dụng.
