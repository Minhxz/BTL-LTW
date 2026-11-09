(() => {
    "use strict";
    const giaoTiep = window.GiaoTiepMayChu.guiYeuCau,
        noiDungTrang = document.querySelector("#content"),
        hopThoai = document.querySelector("#editor"),
        bieuMau = document.querySelector("#editor-form"),
        noiDungHopThoai = document.querySelector("#editor-body"),
        nutLuu = document.querySelector("#save-editor"),
        loi = document.querySelector("#editor-error");
    const vaiTro = document.body.dataset.role,
        laQuanLy = vaiTro === "Manager",
        laThuNgan = vaiTro === "Cashier",
        laTho = vaiTro === "Mechanic",
        duocTiepNhan = laQuanLy || laThuNgan;
    const maHoaHtml = (phanTuXet) =>
        String(phanTuXet ?? "").replace(
            /[&<>"']/g,
            (kyTu) =>
                ({
                    "&": "&amp;",
                    "<": "&lt;",
                    ">": "&gt;",
                    '"': "&quot;",
                    "'": "&#39;",
                })[kyTu],
        );
    const dinhDangTien = (phanTuXet) =>
        new Intl.NumberFormat("vi-VN", {
            style: "currency",
            currency: "VND",
            maximumFractionDigits: 0,
        }).format(Number(phanTuXet) || 0);
    const dinhDangNgay = (phanTuXet) =>
        phanTuXet
            ? new Intl.DateTimeFormat("vi-VN", { timeZone: "Asia/Bangkok" }).format(
                  new Date(String(phanTuXet).slice(0, 10) + "T12:00:00+07:00"),
              )
            : "—";
    const homNay = () =>
        new Intl.DateTimeFormat("sv-SE", { timeZone: "Asia/Bangkok" }).format(new Date());
    let cacCauHinhDanhMuc = [],
        mucDangChon = "dashboard",
        trang = 1,
        tuKhoa = "",
        trangThaiLoc = "",
        tuNgay = "",
        denNgay = "",
        cacDongHienTai = [],
        khiLuu,
        maLanTai = 0,
        boHenThongBao,
        dangLuuHopThoai = false;
    const cacTenMuc = {
        dashboard: "Tổng quan cửa hàng",
        bookings: "Lịch hẹn online",
        jobs: "Tiếp nhận & sửa chữa",
        purchases: "Phiếu nhập kho",
        stockHistory: "Lịch sử điều chỉnh",
        invoices: "Hóa đơn thanh toán",
        reports: "Báo cáo doanh thu",
        myAttendance: "Công cá nhân",
        profile: "Tài khoản cá nhân",
    };
    const cacNhomNhanSu = [
        { title: "Nhân viên & chức vụ", items: ["employees", "roles"] },
        { title: "Ca làm & chấm công", items: ["attendance", "shifts", "myAttendance"] },
        { title: "Thưởng / phạt", items: ["adjustments", "roleAdjustments"] },
    ];
    const duocMoMuc = (maBanGhi) =>
        cacTenMuc[maBanGhi] || cacCauHinhDanhMuc.some((phanTuXet) => phanTuXet.id === maBanGhi);
    const layNhomNhanSu = () => cacNhomNhanSu.find((nhom) => nhom.items.includes(mucDangChon));
    function hienMucNhanSu() {
        const nhom = layNhomNhanSu();
        if (!nhom) return "";
        const cacLienKet = nhom.items.filter(duocMoMuc).map((maBanGhi) => {
            const tieuDeNhap =
                cacTenMuc[maBanGhi] ||
                cacCauHinhDanhMuc.find((phanTuXet) => phanTuXet.id === maBanGhi)?.title;
            return `<a class="personnel-tab ${maBanGhi === mucDangChon ? "active" : ""}" href="#${maBanGhi}"${maBanGhi === mucDangChon ? ' aria-current="page"' : ""}>${maHoaHtml(tieuDeNhap)}</a>`;
        });
        return `<nav class="personnel-tabs" aria-label="${maHoaHtml(nhom.title)}">${cacLienKet.join("")}</nav>`;
    }
    function mauTrangThai(trangThai) {
        if (["Hoàn thành", "Đã tiếp nhận"].includes(trangThai)) return "green";
        if (["Chờ sửa", "Chờ xác nhận"].includes(trangThai)) return "amber";
        if (trangThai === "Đang sửa") return "blue";
        if (trangThai === "Đã hủy") return "red";
        return "";
    }
    const taoNhanTrangThai = (giaTriTrangThai) =>
        `<span class="badge ${mauTrangThai(giaTriTrangThai)}">${maHoaHtml(giaTriTrangThai)}</span>`;
    const taoNut = (noiDung, thaoTac, maBanGhi = "", kieuHienThi = "secondary small") =>
        `<button type="button" class="button ${kieuHienThi}" data-action="${maHoaHtml(thaoTac)}" data-id="${maHoaHtml(maBanGhi)}">${maHoaHtml(noiDung)}</button>`;
    const hienThongBao = (noiDung) => {
        const phanTu = document.querySelector("#toast");
        phanTu.textContent = noiDung;
        phanTu.hidden = false;
        clearTimeout(boHenThongBao);
        boHenThongBao = setTimeout(() => (phanTu.hidden = true), 4500);
    };
    const layLuaChon = async (loaiDanhMuc, giaTriNhap = "", khongBatBuoc = true) => {
        const cacDong = await giaoTiep("/api/catalog/options/" + loaiDanhMuc);
        return (
            (khongBatBuoc ? '<option value="">Chọn…</option>' : "") +
            cacDong
                .map(
                    (phanTuXet) =>
                        `<option value="${maHoaHtml(phanTuXet.id)}" ${String(phanTuXet.id) === String(giaTriNhap) ? "selected" : ""}>${maHoaHtml(phanTuXet.label)} [${maHoaHtml(phanTuXet.id)}]</option>`,
                )
                .join("")
        );
    };
    const taoTruongNhap = (
        ten,
        nhanHienThi,
        kieuNhap = "text",
        giaTriNhap = "",
        thuocTinhThem = "",
    ) =>
        `<label class="field"><span>${maHoaHtml(nhanHienThi)}</span><input name="${maHoaHtml(ten)}" type="${kieuNhap}" value="${maHoaHtml(giaTriNhap)}" ${thuocTinhThem}/></label>`;
    const taoTruongChon = (ten, nhanHienThi, noiDungHtml, thuocTinhThem = "required") =>
        `<label class="field"><span>${maHoaHtml(nhanHienThi)}</span><select name="${ten}" ${thuocTinhThem}>${noiDungHtml}</select></label>`;
    function moHopThoai(tieuDeNhap, noiDungHtml, hamXuLyLuu = null, nhanHienThi = "Lưu thông tin") {
        document.querySelector("#editor-title").textContent = tieuDeNhap;
        noiDungHopThoai.innerHTML = noiDungHtml;
        loi.textContent = "";
        khiLuu = hamXuLyLuu;
        nutLuu.hidden = !hamXuLyLuu;
        nutLuu.textContent = nhanHienThi;
        nutLuu.disabled = false;
        if (!hopThoai.open) hopThoai.showModal();
    }
    function dongHopThoai() {
        if (!dangLuuHopThoai) hopThoai.close();
    }
    document.querySelector("#close-editor").onclick = dongHopThoai;
    document.querySelector("#cancel-editor").onclick = dongHopThoai;
    hopThoai.addEventListener("cancel", (suKien) => {
        if (dangLuuHopThoai) suKien.preventDefault();
    });
    bieuMau.addEventListener("submit", async (suKienNhap) => {
        suKienNhap.preventDefault();
        if (!khiLuu || dangLuuHopThoai || !bieuMau.reportValidity()) return;
        loi.textContent = "";
        nutLuu.disabled = true;
        dangLuuHopThoai = true;
        const hamXuLyLuu = khiLuu;
        try {
            const thongBao = await hamXuLyLuu(new FormData(bieuMau));
            dangLuuHopThoai = false;
            hopThoai.close();
            hienThongBao(thongBao || "Đã lưu thông tin.");
            await taiDuLieu();
        } catch (suKienNhap) {
            loi.textContent = suKienNhap.message;
        } finally {
            nutLuu.disabled = false;
            dangLuuHopThoai = false;
        }
    });
    document.querySelector("#menu-toggle").onclick = () =>
        document.querySelector("#sidebar").classList.toggle("open");
    document.querySelector("#logout").onclick = async () => {
        try {
            await giaoTiep("/api/auth/logout", { method: "POST" });
            location.href = "/Account/Login";
        } catch (suKienNhap) {
            hienThongBao(suKienNhap.message);
        }
    };
    function hienMenu() {
        const cacNhom = [
            [
                "TỔNG QUAN",
                [
                    ["dashboard", "◫"],
                    ...(duocTiepNhan ? [["bookings", "◷"]] : []),
                    ...(duocTiepNhan || laTho ? [["jobs", "▱"]] : []),
                ],
            ],
            [
                "KHÁCH HÀNG & DỊCH VỤ",
                ["customers", "services", "serviceParts"].map((phanTuXet) => [phanTuXet, "◇"]),
            ],
            [
                "KHO & NHÀ CUNG CẤP",
                ["parts", "suppliers", ...(laQuanLy ? ["purchases", "stockHistory"] : [])].map(
                    (phanTuXet) => [phanTuXet, "▦"],
                ),
            ],
            ["NHÂN SỰ", cacNhomNhanSu.map((nhom) => [nhom.items.find(duocMoMuc), "♧"])],
            [
                "TÀI CHÍNH",
                [
                    ...(duocTiepNhan ? [["invoices", "▤"]] : []),
                    ...(laQuanLy ? [["reports", "◴"]] : []),
                    ["profile", "⚙"],
                ],
            ],
        ];
        document.querySelector("#navigation").innerHTML = cacNhom
            .map(([nhanHienThi, cacMucDuLieu]) => {
                cacMucDuLieu = cacMucDuLieu.filter(
                    ([maBanGhi]) =>
                        cacTenMuc[maBanGhi] ||
                        cacCauHinhDanhMuc.some((phanTuXet) => phanTuXet.id === maBanGhi),
                );
                if (!cacMucDuLieu.length) return "";

                const cacMuc = cacMucDuLieu.map(([maBanGhi, bieuTuong]) => {
                    const nhom = cacNhomNhanSu.find((nhom) => nhom.items.includes(maBanGhi));
                    const tenMuc =
                        nhom?.title ||
                        cacTenMuc[maBanGhi] ||
                        cacCauHinhDanhMuc.find((phanTuXet) => phanTuXet.id === maBanGhi)?.title;
                    const dangChon = nhom
                        ? nhom.items.includes(mucDangChon)
                        : maBanGhi === mucDangChon;
                    return `<a class="nav-item ${dangChon ? "active" : ""}" href="#${maBanGhi}"${dangChon ? ' aria-current="page"' : ""}><i>${bieuTuong}</i>${maHoaHtml(tenMuc)}</a>`;
                });

                return `<div class="nav-label">${nhanHienThi}</div>` + cacMuc.join("");
            })
            .join("");
    }
    function taoBoLoc({
        dates: locTheoNgay = false,
        states: locTrangThai = false,
        add: choThem = false,
        extra: thuocTinhThem = "",
        placeholder: noiDungGoiY = "Tìm mã, tên hoặc thông tin…",
    } = {}) {
        const oTimKiem = `<input name="q" class="search" placeholder="${maHoaHtml(noiDungGoiY)}" aria-label="Tìm kiếm" value="${maHoaHtml(tuKhoa)}" maxlength="150"/>`;

        let oTrangThai = "";
        if (locTrangThai) {
            const cacTrangThai = ["Chờ sửa", "Đang sửa", "Hoàn thành", "Đã hủy"]
                .map(
                    (giaTriTrangThai) =>
                        `<option ${giaTriTrangThai === trangThaiLoc ? "selected" : ""}>${giaTriTrangThai}</option>`,
                )
                .join("");
            oTrangThai = `<select name="status" aria-label="Trạng thái"><option value="">Tất cả trạng thái</option>${cacTrangThai}</select>`;
        }

        let oNgay = "";
        if (locTheoNgay) {
            oNgay =
                `<input name="from" aria-label="Từ ngày" type="date" value="${tuNgay}"/>` +
                `<input name="to" aria-label="Đến ngày" type="date" value="${denNgay}"/>`;
        }

        return [
            `<form id="filters" class="toolbar">`,
            oTimKiem,
            oTrangThai,
            oNgay,
            `<button class="button secondary">Lọc</button>`,
            taoNut("Làm mới", "reset"),
            choThem ? taoNut("+ Thêm mới", "create", "", "primary") : "",
            thuocTinhThem,
            `</form>`,
        ].join("");
    }
    function taoBang(cacCotHienThi, cacDong, cacThaoTac) {
        const tieuDe =
            cacCotHienThi.map((kyTu) => `<th>${maHoaHtml(kyTu[1])}</th>`).join("") +
            (cacThaoTac ? "<th>Thao tác</th>" : "");

        const taoDong = (dongDuLieu, viTri) => {
            const cacO = cacCotHienThi
                .map(
                    ([khoa, nhanHienThi, hienKetQua]) =>
                        `<td>${hienKetQua ? hienKetQua(dongDuLieu[khoa], dongDuLieu) : maHoaHtml(dongDuLieu[khoa] ?? "—")}</td>`,
                )
                .join("");
            const oThaoTac = cacThaoTac
                ? `<td><div class="row-actions">${cacThaoTac(dongDuLieu, viTri)}</div></td>`
                : "";
            return `<tr>${cacO}${oThaoTac}</tr>`;
        };

        const soCot = cacCotHienThi.length + (cacThaoTac ? 1 : 0);
        const dongTrong = `<tr><td colspan="${soCot}"><div class="empty">Chưa có dữ liệu phù hợp. Thử thay bộ lọc hoặc thêm bản ghi mới.</div></td></tr>`;
        const thanBang = cacDong.length ? cacDong.map(taoDong).join("") : dongTrong;

        return `<div class="table-wrap"><table><thead><tr>${tieuDe}</tr></thead><tbody>${thanBang}</tbody></table></div>`;
    }

    function taoPhanTrang(duLieuTraVe) {
        const tongSoTrang = duLieuTraVe.totalPages || 1;
        return [
            `<div class="pager">`,
            `<span class="count">${duLieuTraVe.total ?? duLieuTraVe.items.length} bản ghi</span>`,
            `<button class="button secondary small" data-action="prev" ${trang <= 1 ? "disabled" : ""}>← Trước</button>`,
            `<span>Trang ${trang} / ${tongSoTrang}</span>`,
            `<button class="button secondary small" data-action="next" ${trang >= tongSoTrang ? "disabled" : ""}>Sau →</button>`,
            `</div>`,
        ].join("");
    }
    function layBoLoc() {
        return new URLSearchParams({
            q: tuKhoa,
            page: trang,
            pageSize: 12,
            status: trangThaiLoc,
            from: tuNgay,
            to: denNgay,
        });
    }
    async function taiDuLieu() {
        const lanTai = ++maLanTai;
        document.querySelector("#page-title").textContent =
            layNhomNhanSu()?.title ||
            cacTenMuc[mucDangChon] ||
            cacCauHinhDanhMuc.find((phanTuXet) => phanTuXet.id === mucDangChon)?.title ||
            "Quản lý";
        hienMenu();
        const dieuHuongNhanSu = hienMucNhanSu();
        noiDungTrang.innerHTML = dieuHuongNhanSu + '<div class="empty">Đang đọc dữ liệu…</div>';
        try {
            let noiDungHtml = "";
            if (mucDangChon === "dashboard") noiDungHtml = await hienTongQuan();
            else if (mucDangChon === "reports") noiDungHtml = await hienBaoCao();
            else if (mucDangChon === "profile")
                noiDungHtml = [
                    `<div class="panel">`,
                    `<h2>Đổi mật khẩu</h2>`,
                    `<p class="hint">Mật khẩu được cập nhật cho tài khoản dùng chung của nhóm trên cả website và ứng dụng quản lý.</p>`,
                    taoNut("Đổi mật khẩu", "password", "", "primary"),
                    `</div>`,
                ].join("");
            else if (mucDangChon === "myAttendance") {
                const duLieu = await giaoTiep("/api/workshop/my-attendance?" + layBoLoc());
                noiDungHtml = `<div class="panel"><h2>Tổng giờ thực tế: ${maHoaHtml(duLieu.totalHours)}</h2>${taoBoLoc({ dates: true })}${taoBang(
                    [
                        ["date", "Ngày", dinhDangNgay],
                        ["shift", "Ca"],
                        ["start", "Bắt đầu"],
                        ["end", "Kết thúc"],
                        ["hours", "Giờ thực tế"],
                    ],
                    duLieu.items,
                )}</div>`;
            } else {
                const cauHinhDanhMuc = cacCauHinhDanhMuc.find(
                    (phanTuXet) => phanTuXet.id === mucDangChon,
                );
                let duongDanApi = cauHinhDanhMuc
                    ? "/api/catalog/" + mucDangChon
                    : {
                          jobs: "/api/workshop/jobs",
                          bookings: "/api/workshop/bookings",
                          purchases: "/api/workshop/stock/purchases",
                          stockHistory: "/api/workshop/stock/history",
                          invoices: "/api/workshop/invoices",
                      }[mucDangChon];
                if (!duongDanApi) {
                    location.hash = "dashboard";
                    return;
                }
                const duLieu = await giaoTiep(duongDanApi + "?" + layBoLoc());
                if (lanTai !== maLanTai) return;
                cacDongHienTai = duLieu.items;
                if (mucDangChon === "customers") {
                    noiDungHtml =
                        taoBoLoc({ placeholder: "Tìm mã, tên, số điện thoại hoặc biển số…" }) +
                        (duLieu.items.length
                            ? taoBang(
                                  [
                                      ["MaKhachHang", "Mã khách hàng"],
                                      ["TenKhachHang", "Tên khách hàng"],
                                      ["SoDienThoai", "Số điện thoại"],
                                      ["vehicleCount", "Số xe"],
                                  ],
                                  duLieu.items,
                                  (_, chiSo) =>
                                      taoNut("Xem xe", "customer-vehicles", chiSo) +
                                      taoNut("Sửa", "edit", chiSo),
                              )
                            : '<div class="empty">Không tìm thấy khách hàng phù hợp.</div>');
                } else if (cauHinhDanhMuc) {
                    const cacCot = cauHinhDanhMuc.fields
                        .filter((truongNhap) => truongNhap.type !== "password")
                        .map((truongNhap) => [
                            truongNhap.name,
                            truongNhap.label,
                            (giaTriTruong) =>
                                truongNhap.type === "bool"
                                    ? taoNhanTrangThai(
                                          giaTriTruong ? "Đang hoạt động" : "Ngừng hoạt động",
                                      )
                                    : truongNhap.type === "date"
                                      ? dinhDangNgay(giaTriTruong)
                                      : truongNhap.type === "money" ||
                                          truongNhap.type === "signedMoney"
                                        ? dinhDangTien(giaTriTruong)
                                        : maHoaHtml(giaTriTruong),
                        ]);
                    noiDungHtml =
                        taoBoLoc({
                            add: true,
                            extra:
                                mucDangChon === "parts"
                                    ? taoNut("Điều chỉnh tồn", "stock-adjust")
                                    : "",
                        }) +
                        taoBang(cacCot, duLieu.items, (banGhi, chiSo) => {
                            const coTheSua = cauHinhDanhMuc.fields.some(
                                (truongNhap) =>
                                    !truongNhap.readOnly &&
                                    !cauHinhDanhMuc.keys.includes(truongNhap.name),
                            );
                            const nutSua = coTheSua ? taoNut("Sửa", "edit", chiSo) : "";
                            const nutNoiDung =
                                mucDangChon === "services"
                                    ? taoNut("Ảnh & nội dung", "content", banGhi.MaDichVu)
                                    : "";
                            return nutSua + nutNoiDung + taoNut("Xóa", "delete", chiSo);
                        });
                }
                if (mucDangChon === "jobs")
                    noiDungHtml =
                        taoBoLoc({ dates: true, states: true, add: duocTiepNhan }) +
                        taoBang(
                            [
                                ["id", "Mã phiếu"],
                                [
                                    "customerName",
                                    "Khách hàng",
                                    (giaTriTruong, banGhi) =>
                                        `${maHoaHtml(giaTriTruong)}<span class="cell-secondary">${maHoaHtml(banGhi.phone)}</span>`,
                                ],
                                [
                                    "licensePlate",
                                    "Xe",
                                    (giaTriTruong, banGhi) =>
                                        `<b>${maHoaHtml(giaTriTruong)}</b><span class="cell-secondary">${maHoaHtml(banGhi.brand)}</span>`,
                                ],
                                ["dueDate", "Hẹn trả", dinhDangNgay],
                                ["status", "Tiến độ", taoNhanTrangThai],
                                ["total", "Tổng tiền", dinhDangTien],
                                [
                                    "paid",
                                    "Thu tiền",
                                    (giaTriTruong) =>
                                        taoNhanTrangThai(giaTriTruong ? "Đã thu" : "Chưa thu"),
                                ],
                            ],
                            duLieu.items,
                            (banGhi) => taoNut("Chi tiết", "job", banGhi.id),
                        );
                if (mucDangChon === "bookings")
                    noiDungHtml =
                        taoBoLoc() +
                        taoBang(
                            [
                                ["id", "Mã lịch"],
                                [
                                    "customerName",
                                    "Khách hàng",
                                    (giaTriTruong, banGhi) =>
                                        `${maHoaHtml(giaTriTruong)}<span class="cell-secondary">${maHoaHtml(banGhi.phone)}</span>`,
                                ],
                                ["licensePlate", "Biển số"],
                                ["service", "Dịch vụ"],
                                [
                                    "date",
                                    "Ngày / giờ hẹn",
                                    (giaTriTruong) =>
                                        `${dinhDangNgay(giaTriTruong)} · ${maHoaHtml(String(giaTriTruong).slice(11, 16))}`,
                                ],
                                ["note", "Yêu cầu"],
                                ["status", "Trạng thái", taoNhanTrangThai],
                            ],
                            duLieu.items,
                            (banGhi) =>
                                banGhi.status === "Chờ xác nhận"
                                    ? taoNut("Tiếp nhận", "accept", banGhi.id) +
                                      taoNut("Hủy", "reject", banGhi.id)
                                    : banGhi.jobId
                                      ? taoNut("Xem phiếu", "job", banGhi.jobId)
                                      : "",
                        );
                if (mucDangChon === "purchases")
                    noiDungHtml =
                        taoBoLoc({ add: true }) +
                        taoBang(
                            [
                                ["id", "Mã phiếu"],
                                ["date", "Ngày nhập", dinhDangNgay],
                                ["employee", "Nhân viên"],
                                ["note", "Ghi chú"],
                                ["amount", "Tổng nhập", dinhDangTien],
                            ],
                            duLieu.items,
                            (banGhi) => taoNut("Chi tiết", "purchase-detail", banGhi.id),
                        );
                if (mucDangChon === "stockHistory")
                    noiDungHtml =
                        taoBoLoc() +
                        taoBang(
                            [
                                ["id", "Mã"],
                                ["partId", "Phụ tùng"],
                                ["kind", "Nội dung"],
                                ["date", "Ngày", dinhDangNgay],
                                ["oldValue", "Trước"],
                                ["newValue", "Sau"],
                                ["reason", "Lý do"],
                                ["employee", "Người thực hiện"],
                            ],
                            duLieu.items,
                        );
                if (mucDangChon === "invoices")
                    noiDungHtml =
                        taoBoLoc({ dates: true }) +
                        `<p class="hint">Tổng tiền trong kết quả lọc: <b>${dinhDangTien(duLieu.totalAmount)}</b>. ` +
                        `Lập hóa đơn từ chi tiết phiếu sửa đã hoàn thành.</p>` +
                        taoBang(
                            [
                                ["id", "Hóa đơn"],
                                ["jobId", "Phiếu sửa"],
                                ["customerName", "Khách hàng"],
                                ["licensePlate", "Biển số"],
                                ["paidOn", "Ngày thu", dinhDangNgay],
                                ["amount", "Thành tiền", dinhDangTien],
                            ],
                            duLieu.items,
                            (banGhi) => taoNut("Xem / In", "print-invoice", banGhi.jobId),
                        );
                noiDungHtml = `<section class="panel">${noiDungHtml}${taoPhanTrang(duLieu)}</section>`;
            }
            if (lanTai !== maLanTai) return;
            noiDungTrang.innerHTML = dieuHuongNhanSu + noiDungHtml;
            document.querySelector("#filters")?.addEventListener("submit", (suKienNhap) => {
                suKienNhap.preventDefault();
                const duLieu = new FormData(suKienNhap.target);
                tuKhoa = String(duLieu.get("q") || "");
                trangThaiLoc = String(duLieu.get("status") || "");
                tuNgay = String(duLieu.get("from") || "");
                denNgay = String(duLieu.get("to") || "");
                trang = 1;
                taiDuLieu();
            });
        } catch (suKienNhap) {
            if (lanTai === maLanTai)
                noiDungTrang.innerHTML =
                    dieuHuongNhanSu +
                    `<div class="error-box">${maHoaHtml(suKienNhap.message)} ${taoNut("Thử lại", "reload")}</div>`;
        }
    }
    async function hienTongQuan() {
        const duLieu = await giaoTiep("/api/workshop/dashboard");
        document.querySelector("#db-status").textContent = "● " + duLieu.database;
        const demSoLuong = (giaTriTrangThai) =>
            duLieu.jobs.find((phanTuXet) => phanTuXet.status === giaTriTrangThai)?.count || 0;
        function oSoLieu(nhan, giaTri, ghiChu, kieu = "", kieuHienThi = "") {
            const lop = kieu ? `stat ${kieu}` : "stat";
            const thuocTinhStyle = kieuHienThi ? ` style="${kieuHienThi}"` : "";
            return `<article class="${lop}"><label>${nhan}</label><strong${thuocTinhStyle}>${giaTri}</strong><small>${ghiChu}</small></article>`;
        }
        const oThu3 = duocTiepNhan
            ? oSoLieu("LỊCH CHỜ XÁC NHẬN", duLieu.pendingBookings, "Yêu cầu từ website")
            : oSoLieu("ĐÃ HOÀN THÀNH", demSoLuong("Hoàn thành"), "Xe được giao phụ trách");
        const oThu4 = duocTiepNhan
            ? oSoLieu(
                  "THU HÔM NAY",
                  dinhDangTien(duLieu.todayRevenue),
                  "Từ hóa đơn đã thanh toán",
                  "",
                  "font-size:25px",
              )
            : oSoLieu(
                  "TỔNG PHIẾU",
                  duLieu.jobs.reduce(
                      (giaTriTrangThai, phanTuXet) => giaTriTrangThai + phanTuXet.count,
                      0,
                  ),
                  "Trong phạm vi của bạn",
                  "",
                  "font-size:25px",
              );

        const loiChao = [
            `<div class="welcome">`,
            `<div><h2>Mọi hoạt động, trong một nơi.</h2><p class="muted">Theo dõi cửa hàng và tiếp tục công việc hôm nay.</p></div>`,
            duocTiepNhan ? taoNut("+ Tiếp nhận xe", "create-job", "", "primary") : "",
            `</div>`,
        ].join("");

        const soLieu = [
            `<div class="stats">`,
            oSoLieu("XE ĐANG SỬA", demSoLuong("Đang sửa"), "Đang được thợ xử lý", "dark"),
            oSoLieu("CHỜ SỬA", demSoLuong("Chờ sửa"), "Đã tiếp nhận tại cửa hàng"),
            oThu3,
            oThu4,
            `</div>`,
        ].join("");

        const bangTrangThai = taoBang(
            [
                ["status", "Tiến độ", taoNhanTrangThai],
                ["count", "Số phiếu"],
            ],
            duLieu.jobs,
        );
        const khungTrangThai = `<section class="panel"><div class="panel-head"><h2>Trạng thái phiếu sửa</h2></div>${bangTrangThai}</section>`;
        let khungCanhBaoKho = "";
        if (duLieu.stockAlerts) {
            const bangCanhBao = taoBang(
                [
                    ["id", "Mã"],
                    ["name", "Phụ tùng"],
                    ["stock", "Tồn hiện tại"],
                    ["expiry", "Hạn sử dụng", dinhDangNgay],
                ],
                duLieu.stockAlerts,
            );
            khungCanhBaoKho = [
                `<section class="panel">`,
                `<div class="panel-head"><h2>Phụ tùng cần chú ý</h2><a href="#parts">Đến kho →</a></div>`,
                bangCanhBao,
                `<p class="hint">Tồn không quá 5 hoặc hết hạn trong 30 ngày tới.</p>`,
                `</section>`,
            ].join("");
        }

        return loiChao + soLieu + khungTrangThai + khungCanhBaoKho;
    }
    async function hienBaoCao() {
        if (!tuNgay && !denNgay) {
            tuNgay = homNay().slice(0, 8) + "01";
            denNgay = homNay();
        }
        const duLieu = await giaoTiep("/api/workshop/reports?" + layBoLoc());
        const gioiHanToiDa = Math.max(1, ...duLieu.daily.map((phanTuXet) => phanTuXet.amount));

        const duongDanCsv = `/api/workshop/reports/export?${new URLSearchParams({ from: tuNgay, to: denNgay })}`;
        const boLoc = taoBoLoc({
            dates: true,
            extra: `<a class="button primary" href="${duongDanCsv}">Tải CSV</a>`,
        });

        const oSoLieu = (nhan, soTien, ghiChu, kieu = "stat") =>
            `<article class="${kieu}"><label>${nhan}</label><strong style="font-size:24px">${dinhDangTien(soTien)}</strong><small>${ghiChu}</small></article>`;

        const soLieu = [
            `<div class="stats">`,
            oSoLieu("DOANH THU ĐÃ THU", duLieu.paid, "Theo ngày thanh toán", "stat dark"),
            oSoLieu("GIÁ TRỊ NHẬP KHO", duLieu.purchases, "Theo ngày nhập"),
            oSoLieu("THU TRỪ NHẬP KHO", duLieu.cashDifference, "Không phải lợi nhuận kế toán"),
            oSoLieu("HOÀN THÀNH CHƯA THU", duLieu.unpaid, "Tất cả thời gian"),
            `</div>`,
        ].join("");
        const taoCot = (phanTuXet) => {
            const doDai = Math.max(1, (phanTuXet.amount / gioiHanToiDa) * 100);
            return [
                `<div class="chart-row">`,
                `<span>${dinhDangNgay(phanTuXet.date)}</span>`,
                `<div class="chart-track"><span style="width:${doDai}%"></span></div>`,
                `<strong>${dinhDangTien(phanTuXet.amount)}</strong>`,
                `</div>`,
            ].join("");
        };
        const bieuDo = duLieu.daily.length
            ? duLieu.daily.map(taoCot).join("")
            : '<div class="empty">Chưa có khoản thu trong kỳ.</div>';

        const bangDichVu = taoBang(
            [
                ["name", "Dịch vụ"],
                ["quantity", "Lượt"],
                ["amount", "Doanh thu", dinhDangTien],
            ],
            duLieu.services,
        );

        const haiCot = [
            `<div class="grid-two">`,
            `<section class="panel"><h2>Doanh thu theo ngày</h2><div class="chart-bars">${bieuDo}</div></section>`,
            `<section class="panel"><h2>Dịch vụ được sử dụng</h2>${bangDichVu}</section>`,
            `</div>`,
        ].join("");

        return boLoc + soLieu + haiCot;
    }
    function layKhoa(cauHinhDanhMuc, dongDuLieu) {
        return cauHinhDanhMuc.keys
            .map((tenCot) => {
                const truongNhap = cauHinhDanhMuc.fields.find(
                    (phanTuXet) => phanTuXet.name === tenCot,
                );
                return truongNhap.type === "date"
                    ? String(dongDuLieu[tenCot]).slice(0, 10)
                    : dongDuLieu[tenCot];
            })
            .join("~");
    }
    const goiYMauXe = 'list="motorcycle-models" placeholder="Ví dụ: Vision"';
    const goiYHangXe = 'list="motorcycle-brands" placeholder="Ví dụ: Honda"';
    async function xemXeCuaKhach(khachHang, phanTuDich) {
        phanTuDich.disabled = true;
        try {
            const cacDong = await giaoTiep(
                "/api/catalog/customers/" + encodeURIComponent(khachHang.MaKhachHang) + "/vehicles",
            );
            if (!phanTuDich.isConnected || hopThoai.open) return;
            moHopThoai(
                "Xe của " + khachHang.TenKhachHang,
                cacDong.length
                    ? taoBang(
                          [
                              ["BienXe", "Biển số"],
                              ["LoaiXe", "Mẫu xe"],
                              ["HangSanXuat", "Hãng xe"],
                              ["MauSac", "Màu sắc"],
                          ],
                          cacDong,
                      )
                    : '<div class="empty">Khách hàng chưa có xe.</div>',
            );
        } finally {
            phanTuDich.disabled = false;
        }
    }

    async function suaDanhMuc(viTri) {
        const cauHinhDanhMuc = cacCauHinhDanhMuc.find((phanTuXet) => phanTuXet.id === mucDangChon),
            dongDuLieu = viTri == null ? null : cacDongHienTai[viTri];
        const maDaCap =
            !dongDuLieu && cauHinhDanhMuc.fields.some((truong) => truong.type === "code")
                ? await giaoTiep("/api/catalog/" + cauHinhDanhMuc.id + "/code", { method: "POST" })
                : null;
        const cacTruong = await Promise.all(
            cauHinhDanhMuc.fields
                .filter((truongNhap) => !truongNhap.readOnly)
                .map(async (truongNhap) => {
                    if (truongNhap.type === "code")
                        return `<label class="field"><span>${maHoaHtml(truongNhap.label)}</span><input type="text" value="${maHoaHtml(dongDuLieu?.[truongNhap.name] || maDaCap.id)}" readonly aria-readonly="true"/></label>`;
                    const khoaCoDinh = dongDuLieu && cauHinhDanhMuc.keys.includes(truongNhap.name),
                        giaTriTruong =
                            dongDuLieu?.[truongNhap.name] ??
                            (truongNhap.type === "bool" ? true : "");
                    if (truongNhap.type === "select" && !khoaCoDinh)
                        return taoTruongChon(
                            truongNhap.name,
                            truongNhap.label,
                            await layLuaChon(truongNhap.source, giaTriTruong),
                        );
                    if (truongNhap.type === "textarea") {
                        const oNhap = `<textarea name="${truongNhap.name}" rows="3" maxlength="${truongNhap.max}" required>${maHoaHtml(giaTriTruong)}</textarea>`;
                        return `<label class="field full"><span>${maHoaHtml(truongNhap.label)}</span>${oNhap}</label>`;
                    }
                    if (truongNhap.type === "bool") {
                        const oNhap = `<input name="${truongNhap.name}" type="checkbox" ${giaTriTruong ? "checked" : ""}/>`;
                        return `<label class="field"><span>${maHoaHtml(truongNhap.label)}</span>${oNhap}</label>`;
                    }
                    const kieuNhap =
                        truongNhap.type === "date"
                            ? "date"
                            : truongNhap.type === "time"
                              ? "time"
                              : truongNhap.type === "password"
                                ? "password"
                                : [
                                        "money",
                                        "signedMoney",
                                        "positiveInt",
                                        "int",
                                        "factor",
                                        "hours",
                                    ].includes(truongNhap.type)
                                  ? "number"
                                  : "text";
                    const giaTriNhap =
                        truongNhap.type === "date"
                            ? String(giaTriTruong).slice(0, 10)
                            : truongNhap.type === "time"
                              ? String(giaTriTruong).slice(0, 5)
                              : truongNhap.type === "password"
                                ? ""
                                : giaTriTruong;
                    const gioiHan =
                        truongNhap.type === "money" || truongNhap.type === "positiveInt"
                            ? 'min="1" step="1"'
                            : truongNhap.type === "factor"
                              ? 'min="0.01" max="99.99" step="0.01"'
                              : truongNhap.type === "hours"
                                ? 'min="0" max="24" step="0.01"'
                                : kieuNhap === "number"
                                  ? 'step="1"'
                                  : "";
                    const goiY =
                        { LoaiXe: goiYMauXe, HangSanXuat: goiYHangXe }[truongNhap.name] ?? "";
                    return taoTruongNhap(
                        truongNhap.name,
                        truongNhap.label,
                        kieuNhap,
                        giaTriNhap,
                        `${khoaCoDinh ? "readonly" : ""} ${!truongNhap.optional ? "required" : ""} maxlength="${truongNhap.max}" ${gioiHan} ${goiY}`,
                    );
                }),
        );
        const ghiChuPhuTung =
            mucDangChon === "parts"
                ? '<p class="hint">Phụ tùng mới có tồn 0. Dùng Phiếu nhập để tăng tồn hoặc Điều chỉnh tồn khi kiểm kê.</p>'
                : "";
        if (mucDangChon !== cauHinhDanhMuc.id) return;
        moHopThoai(
            (dongDuLieu ? "Cập nhật · " : "Thêm · ") + cauHinhDanhMuc.title,
            `<div class="form-grid">${cacTruong.join("")}</div>${ghiChuPhuTung}`,
            async (duLieu) => {
                const duLieuGui = Object.fromEntries(duLieu);
                if (maDaCap) duLieuGui.maDaCap = maDaCap.ticket;
                for (const truongNhap of cauHinhDanhMuc.fields)
                    if (truongNhap.type === "bool")
                        duLieuGui[truongNhap.name] = bieuMau.elements[truongNhap.name].checked;
                const banGhi = await giaoTiep(
                    "/api/catalog/" +
                        cauHinhDanhMuc.id +
                        (dongDuLieu
                            ? "/" + encodeURIComponent(layKhoa(cauHinhDanhMuc, dongDuLieu))
                            : ""),
                    { method: dongDuLieu ? "PUT" : "POST", body: JSON.stringify(duLieuGui) },
                );
                return banGhi.message;
            },
        );
    }
    const giaoDienXemTruocPhieu = `<div class="detail-actions"><button class="button secondary" type="button" id="job-preview-button">Kiểm tra tiền và phụ tùng</button></div>
    <div id="job-preview" aria-live="polite"></div>`;
    const dongDichVuHopLe = (dongChiTiet) =>
        dongChiTiet.serviceId &&
        Number.isInteger(dongChiTiet.quantity) &&
        dongChiTiet.quantity >= 1 &&
        dongChiTiet.quantity <= 1000;

    function ganXemTruocPhieu(layDuLieu) {
        const nut = noiDungHopThoai.querySelector("#job-preview-button");
        const vungKetQua = noiDungHopThoai.querySelector("#job-preview");
        nut.addEventListener("click", async () => {
            const duLieuNhap = layDuLieu();
            const duDuLieu =
                duLieuNhap.vehicleType &&
                duLieuNhap.services.length &&
                duLieuNhap.services.every(dongDichVuHopLe);
            if (!duDuLieu) {
                vungKetQua.textContent =
                    "Nhập mẫu xe, chọn dịch vụ và số lượng hợp lệ để kiểm tra.";
                return;
            }
            const guiYeuCau = JSON.stringify(duLieuNhap);
            const conHienTai = () =>
                nut.isConnected && hopThoai.open && guiYeuCau === JSON.stringify(layDuLieu());
            nut.disabled = true;
            vungKetQua.textContent = "Đang kiểm tra…";
            try {
                const ketQua = await giaoTiep("/api/workshop/jobs/preview", {
                    method: "POST",
                    body: guiYeuCau,
                });
                if (!conHienTai()) return;
                const cacPhuTung = ketQua.parts.map((phuTung) => ({
                    ...phuTung,
                    issue: phuTung.issue || "Đủ",
                }));
                vungKetQua.innerHTML = `<div class="invoice-total">Tạm tính <b>${dinhDangTien(ketQua.total)}</b></div>${
                    cacPhuTung.length
                        ? taoBang(
                              [
                                  ["id", "Mã phụ tùng"],
                                  ["name", "Phụ tùng"],
                                  ["quantity", "Cần dùng"],
                                  ["stock", "Tồn kho"],
                                  ["issue", "Kiểm tra"],
                              ],
                              cacPhuTung,
                          )
                        : '<p class="hint">Các dịch vụ đã chọn không có định mức phụ tùng.</p>'
                }
          <p class="hint">Kho và giá được kiểm tra lại khi lưu phiếu.</p>`;
            } catch (suKienNhap) {
                if (conHienTai()) vungKetQua.textContent = suKienNhap.message;
            } finally {
                nut.disabled = false;
            }
        });
        return () => vungKetQua.replaceChildren();
    }

    async function tiepNhanXe() {
        const [cacLuaChonDichVu, cacTho] = await Promise.all([
            layLuaChon("services"),
            layLuaChon("mechanics"),
        ]);
        const layDuLieu = () => ({
            vehicleType: bieuMau.elements.vehicleType.value.trim(),
            services: [...noiDungHopThoai.querySelectorAll("#lines .line-row")].map(
                (dongDuLieu) => ({
                    serviceId: dongDuLieu.querySelector("select").value,
                    quantity: Number(dongDuLieu.querySelector("input").value),
                }),
            ),
        });
        const cacTruong = [
            taoTruongNhap(
                "customerName",
                "Họ tên khách hàng",
                "text",
                "",
                'required maxlength="100"',
            ),
            taoTruongNhap(
                "phone",
                "Số điện thoại",
                "tel",
                "",
                'required pattern="0[0-9]{8,10}" maxlength="11"',
            ),
            taoTruongNhap("licensePlate", "Biển số xe", "text", "", 'required maxlength="20"'),
            taoTruongNhap(
                "vehicleType",
                "Mẫu xe",
                "text",
                "",
                `required maxlength="50" ${goiYMauXe}`,
            ),
            taoTruongNhap("brand", "Hãng xe", "text", "", `required maxlength="50" ${goiYHangXe}`),
            taoTruongNhap("color", "Màu sắc", "text", "", 'required maxlength="30"'),
            taoTruongNhap(
                "dueDate",
                "Ngày hẹn trả",
                "date",
                homNay(),
                `required min="${homNay()}"`,
            ),
            taoTruongChon("mechanicId", "Phân công thợ (có thể bổ sung sau)", cacTho, ""),
        ].join("");

        const noiDungForm = [
            `<div class="form-grid">${cacTruong}</div>`,
            `<h3 style="margin-top:24px">Dịch vụ thực hiện</h3>`,
            `<div id="job-services"><div class="line-items" id="lines"></div>${taoNut("+ Thêm dịch vụ", "line-add")}</div>`,
            giaoDienXemTruocPhieu,
        ].join("");

        moHopThoai(
            "Tiếp nhận xe",
            noiDungForm,
            async (duLieu) => {
                const duLieuNhap = Object.fromEntries(duLieu);
                Object.assign(duLieuNhap, layDuLieu());
                delete duLieuNhap.line;
                const banGhi = await giaoTiep("/api/workshop/jobs", {
                    method: "POST",
                    body: JSON.stringify(duLieuNhap),
                });
                return banGhi.message + " Mã " + banGhi.id;
            },
            "Tiếp nhận và trừ kho",
        );
        noiDungHopThoai.dataset.lineOptions = cacLuaChonDichVu;
        themDong();
        const xoaXemTruoc = ganXemTruocPhieu(layDuLieu);
        bieuMau.elements.vehicleType.addEventListener("input", xoaXemTruoc);
        const cacTruongDichVu = noiDungHopThoai.querySelector("#job-services");
        cacTruongDichVu.addEventListener("input", xoaXemTruoc);
        cacTruongDichVu.addEventListener("click", (suKienNhap) => {
            if (suKienNhap.target.closest('[data-action="line-add"], [data-action="line-remove"]'))
                xoaXemTruoc();
        });
    }
    function themDong() {
        const danhSachDong = document.querySelector("#lines");
        const soLuongToiDa = Number(danhSachDong.dataset.max) || 1000;

        const dongMoi = `
      <div class="line-row">
        <select required aria-label="Dịch vụ hoặc phụ tùng">${noiDungHopThoai.dataset.lineOptions}</select>
        <input aria-label="Số lượng" type="number" value="1" min="1" max="${soLuongToiDa}" step="1" required />
        <button type="button" data-action="line-remove" aria-label="Xóa dòng">×</button>
      </div>`;

        danhSachDong.insertAdjacentHTML("beforeend", dongMoi);
    }
    async function nhapKho() {
        const luaChonPhuTung = await layLuaChon("parts");

        const noiDungForm = `
      <div class="form-grid">
        ${taoTruongNhap("date", "Ngày nhập", "date", homNay(), `required max="${homNay()}"`)}
        ${taoTruongNhap("note", "Ghi chú", "text", "", 'maxlength="255"')}
      </div>
      <h3 style="margin-top:24px">Phụ tùng nhập</h3>
      <div class="line-items" id="lines" data-max="100000"></div>
      ${taoNut("+ Thêm phụ tùng", "line-add")}
      <p class="hint">
        Đơn giá lấy từ lô phụ tùng. Tạo mã lô mới khi giá nhập hoặc hạn sử dụng thay đổi.
        Phiếu đã ghi sổ giữ nguyên; sai lệch được xử lý bằng điều chỉnh tồn có lý do.
      </p>`;

        async function luuPhieuNhap(duLieuForm) {
            const phieuNhap = Object.fromEntries(duLieuForm);
            phieuNhap.lines = [...noiDungHopThoai.querySelectorAll(".line-row")].map((dong) => ({
                partId: dong.querySelector("select").value,
                quantity: Number(dong.querySelector("input").value),
            }));

            const ketQua = await giaoTiep("/api/workshop/stock/purchases", {
                method: "POST",
                body: JSON.stringify(phieuNhap),
            });
            return ketQua.message;
        }

        moHopThoai("Lập phiếu nhập kho", noiDungForm, luuPhieuNhap, "Lưu phiếu và cộng kho");

        noiDungHopThoai.dataset.lineOptions = luaChonPhuTung;
        themDong();
    }
    async function xemPhieuNhap(maPhieuNhap) {
        const duLieu = await giaoTiep(
            "/api/workshop/stock/purchases/" + encodeURIComponent(maPhieuNhap),
        );

        let tongTienNhap = 0;
        for (const dong of duLieu.lines) {
            tongTienNhap += Number(dong.amount);
        }

        const bangChiTiet = taoBang(
            [
                ["partId", "Mã phụ tùng"],
                ["name", "Tên"],
                ["quantity", "Số lượng"],
                ["price", "Đơn giá nhập", dinhDangTien],
                ["amount", "Thành tiền", dinhDangTien],
            ],
            duLieu.lines,
        );

        moHopThoai(
            "Chi tiết phiếu nhập " + maPhieuNhap,
            bangChiTiet +
                `<p class="hint"><strong>Tổng nhập: ${dinhDangTien(tongTienNhap)}</strong></p>`,
        );
    }
    async function kiemKeTon() {
        const luaChonPhuTung = await layLuaChon("parts");

        const noiDungForm = `
      <div class="form-grid">
        ${taoTruongChon("partId", "Phụ tùng", luaChonPhuTung)}
        ${taoTruongNhap("stock", "Số tồn thực tế", "number", "", 'required min="0" max="1000000" step="1"')}
        ${taoTruongNhap("reason", "Lý do điều chỉnh", "text", "", 'required minlength="5" maxlength="255"')}
      </div>`;

        async function luuKiemKe(duLieuForm) {
            const kiemKe = Object.fromEntries(duLieuForm);
            kiemKe.stock = Number(kiemKe.stock);

            const ketQua = await giaoTiep("/api/workshop/stock/adjust", {
                method: "POST",
                body: JSON.stringify(kiemKe),
            });
            return ketQua.message;
        }

        moHopThoai("Điều chỉnh tồn sau kiểm kê", noiDungForm, luuKiemKe);
    }
    async function xemPhieu(maBanGhi, chiHoaDon = false) {
        const duLieu = await giaoTiep("/api/workshop/jobs/" + encodeURIComponent(maBanGhi)),
            chiSoPhu = duLieu.job;
        const dauHoaDon = chiHoaDon
            ? [
                  `<div class="invoice-heading">`,
                  `<span class="eyebrow">CỬA HÀNG SỬA XE · DỊCH VỤ SỬA XE</span>`,
                  `<h2>HÓA ĐƠN THANH TOÁN</h2>`,
                  `<p>${maHoaHtml(duLieu.invoice?.id || "")}</p>`,
                  `</div>`,
              ].join("")
            : "";

        const thongTinPhieu = [
            ["Mã phiếu", chiSoPhu.id],
            ["Khách hàng", chiSoPhu.customerName],
            ["Số điện thoại", chiSoPhu.phone],
            ["Biển số", chiSoPhu.licensePlate],
            ["Ngày nhận", dinhDangNgay(chiSoPhu.createdOn)],
            ["Hẹn trả", dinhDangNgay(chiSoPhu.dueDate)],
        ]
            .map(
                ([tenCot, giaTriTruong]) =>
                    `<div><label>${tenCot}</label><b>${maHoaHtml(giaTriTruong)}</b></div>`,
            )
            .join("");

        const bangDichVu = taoBang(
            [
                ["serviceId", "Mã dịch vụ"],
                ["name", "Tên dịch vụ"],
                ["quantity", "Số lượng"],
                ["amount", "Thành tiền", dinhDangTien],
            ],
            duLieu.lines,
        );

        let noiDungHtml =
            dauHoaDon +
            `<div class="detail-grid">${thongTinPhieu}</div>` +
            bangDichVu +
            `<div class="invoice-total">Tổng cộng: <b>${dinhDangTien(chiSoPhu.total)}</b></div>`;

        if (chiHoaDon) {
            noiDungHtml += [
                `<p>Ngày thanh toán: <b>${dinhDangNgay(duLieu.invoice?.paidOn)}</b></p>`,
                `<p class="hint">Hóa đơn nội bộ phục vụ bài tập, không phải hóa đơn điện tử thuế.</p>`,
                `<div class="detail-actions no-print">${taoNut("In hóa đơn", "print", "", "primary")}</div>`,
            ].join("");
            moHopThoai("Hóa đơn · " + (duLieu.invoice?.id || chiSoPhu.id), noiDungHtml);
            return;
        }
        const ngayGio = (giaTriTruong) =>
            `${dinhDangNgay(giaTriTruong)} ${maHoaHtml(String(giaTriTruong).slice(11, 16))}`;
        const nutBoPhanCong =
            duocTiepNhan && chiSoPhu.status === "Chờ sửa"
                ? (banGhi) =>
                      taoNut("Bỏ phân công", "unassign", chiSoPhu.id + "~" + banGhi.employeeId)
                : null;
        const bangPhanCong = taoBang(
            [
                ["name", "Nhân viên"],
                ["start", "Bắt đầu", ngayGio],
                ["end", "Dự kiến xong", ngayGio],
            ],
            duLieu.assignments,
            nutBoPhanCong,
        );
        const dangXuLy = ["Chờ sửa", "Đang sửa"].includes(chiSoPhu.status);
        const cacNut = [
            duocTiepNhan && dangXuLy
                ? taoNut("Phân công / đổi giờ", "assign", maBanGhi) +
                  taoNut("Sửa ngày hẹn", "due-date", maBanGhi)
                : "",
            chiSoPhu.status === "Chờ sửa"
                ? taoNut("Bắt đầu sửa", "status", maBanGhi + "~Đang sửa", "primary")
                : "",
            chiSoPhu.status === "Đang sửa"
                ? taoNut("Hoàn thành", "status", maBanGhi + "~Hoàn thành", "primary")
                : "",
            duocTiepNhan && chiSoPhu.status === "Chờ sửa"
                ? taoNut("Hủy và hoàn kho", "status", maBanGhi + "~Đã hủy", "danger")
                : "",
            duocTiepNhan && chiSoPhu.status === "Hoàn thành" && !chiSoPhu.paid
                ? taoNut("Lập hóa đơn / Thu tiền", "payment", maBanGhi, "primary")
                : "",
            duocTiepNhan && chiSoPhu.paid
                ? taoNut("Xem / In hóa đơn", "print-invoice", maBanGhi)
                : "",
        ].join("");

        noiDungHtml += `<h3>Phân công thợ</h3>${bangPhanCong}<div class="detail-actions">${cacNut}</div>`;
        moHopThoai("Phiếu " + maBanGhi + " · " + chiSoPhu.status, noiDungHtml);
    }
    async function nhanLich(maBanGhi) {
        const booking = cacDongHienTai.find((dongDuLieu) => dongDuLieu.id === maBanGhi);
        if (!booking) throw new Error("Lịch hẹn không còn trong danh sách. Hãy tải lại.");
        const cacTho = await layLuaChon("mechanics");
        const cacTruong = [
            taoTruongNhap(
                "dueDate",
                "Ngày hẹn trả",
                "date",
                homNay(),
                `required min="${homNay()}"`,
            ),
            taoTruongNhap("color", "Màu xe", "text", "", 'required maxlength="30"'),
            taoTruongChon("mechanicId", "Thợ phụ trách", cacTho, ""),
        ].join("");

        const noiDungForm = [
            `<p>Xe <b>${maHoaHtml(booking.brand)} ${maHoaHtml(booking.vehicleType)}</b> · ${maHoaHtml(booking.licensePlate)}</p>`,
            `<p>Dịch vụ <b>${maHoaHtml(booking.service)}</b></p>`,
            `<div class="form-grid">${cacTruong}</div>`,
            giaoDienXemTruocPhieu,
        ].join("");

        moHopThoai(
            "Tiếp nhận lịch " + maBanGhi,
            noiDungForm,
            async (duLieu) =>
                (
                    await giaoTiep(
                        "/api/workshop/bookings/" + encodeURIComponent(maBanGhi) + "/accept",
                        { method: "POST", body: JSON.stringify(Object.fromEntries(duLieu)) },
                    )
                ).message,
            "Tiếp nhận và trừ kho",
        );
        ganXemTruocPhieu(() => ({
            vehicleType: booking.vehicleType,
            services: [{ serviceId: booking.serviceId, quantity: 1 }],
        }));
    }
    function xacNhan(tieuDeNhap, noiDung, hamXuLy) {
        moHopThoai(
            tieuDeNhap,
            `<p>${maHoaHtml(noiDung)}</p>`,
            async () => {
                const banGhi = await hamXuLy();
                return banGhi?.message;
            },
            "Xác nhận",
        );
    }
    function locNoiDung(giaTriNhap) {
        giaTriNhap = String(giaTriNhap || "")
            .replace(/<(\/?)b\s*>/gi, "<$1strong>")
            .replace(/<(\/?)i\s*>/gi, "<$1em>");
        const taiLieu = new DOMParser().parseFromString(String(giaTriNhap || ""), "text/html");
        const cacTheChoPhep = new Set(["P", "BR", "STRONG", "EM", "UL", "OL", "LI"]);
        function saoChep(soLuong) {
            if (soLuong.nodeType === 3) return document.createTextNode(soLuong.textContent);
            const phanTu = cacTheChoPhep.has(soLuong.nodeName)
                ? document.createElement(soLuong.nodeName.toLowerCase())
                : document.createDocumentFragment();
            if (["SCRIPT", "STYLE", "IFRAME", "SVG", "OBJECT"].includes(soLuong.nodeName))
                return phanTu;
            for (const phanTuCon of soLuong.childNodes) phanTu.append(saoChep(phanTuCon));
            return phanTu;
        }
        const khungBao = document.createElement("div");
        for (const soLuong of taiLieu.body.childNodes) khungBao.append(saoChep(soLuong));
        return khungBao.innerHTML;
    }
    async function suaNoiDungDichVu(maBanGhi) {
        const duLieu = await giaoTiep(
            "/api/workshop/services/" + encodeURIComponent(maBanGhi) + "/content",
        );
        const ghiChuAnh = duLieu.imageUrl ? "Đang dùng ảnh đã lưu." : "Chưa có ảnh riêng.";
        const thanhCongCu =
            taoNut("B · Đậm", "bold") +
            taoNut("I · Nghiêng", "italic") +
            taoNut("• Danh sách", "list");

        const noiDungForm = [
            `<label class="field"><span>Ảnh JPG/PNG/WebP, tối đa 2 MB</span><input type="file" id="service-image" accept="image/jpeg,image/png,image/webp"/></label>`,
            `<input type="hidden" name="imageUrl" value="${maHoaHtml(duLieu.imageUrl)}"/>`,
            `<div id="image-note" class="hint">${ghiChuAnh}</div>`,
            `<div class="editor-tools">${thanhCongCu}</div>`,
            `<textarea id="mo-ta-html" name="description" hidden></textarea>`,
            `<div contenteditable="true" role="textbox" aria-label="Nội dung dịch vụ" class="content-editor" id="rich-editor">${locNoiDung(duLieu.description)}</div>`,
            `<p class="hint">Nội dung hiển thị công khai ở trang dịch vụ. Chỉ giữ các định dạng văn bản an toàn.</p>`,
        ].join("");

        moHopThoai(
            "Ảnh và nội dung dịch vụ",
            noiDungForm,
            async (truongNhap) =>
                (
                    await giaoTiep(
                        "/api/workshop/services/" + encodeURIComponent(maBanGhi) + "/content",
                        {
                            method: "PUT",
                            body: JSON.stringify({
                                description: (document.querySelector("#mo-ta-html").value =
                                    locNoiDung(document.querySelector("#rich-editor").innerHTML)),
                                imageUrl: truongNhap.get("imageUrl"),
                            }),
                        },
                    )
                ).message,
        );
        document.querySelector("#service-image").onchange = async (suKienNhap) => {
            if (!suKienNhap.target.files[0]) return;
            const truongNhap = new FormData();
            truongNhap.append("image", suKienNhap.target.files[0]);
            nutLuu.disabled = true;
            try {
                const ketQua = await giaoTiep("/api/services/image", {
                    method: "POST",
                    body: truongNhap,
                });
                bieuMau.elements.imageUrl.value = ketQua.imageUrl;
                document.querySelector("#image-note").textContent =
                    "Ảnh đã tải lên. Bấm Lưu thông tin để áp dụng.";
            } catch (suKienNhap) {
                loi.textContent = suKienNhap.message;
            } finally {
                nutLuu.disabled = false;
            }
        };
    }
    async function xuLyThaoTac(thaoTac, maBanGhi, phanTuDich) {
        if (thaoTac === "reload") return taiDuLieu();
        if (thaoTac === "reset") {
            tuKhoa = "";
            tuNgay = "";
            denNgay = "";
            trangThaiLoc = "";
            trang = 1;
            return taiDuLieu();
        }
        if (thaoTac === "prev") {
            trang--;
            return taiDuLieu();
        }
        if (thaoTac === "next") {
            trang++;
            return taiDuLieu();
        }
        if (thaoTac === "create")
            return mucDangChon === "jobs"
                ? tiepNhanXe()
                : mucDangChon === "purchases"
                  ? nhapKho()
                  : suaDanhMuc(null);
        if (thaoTac === "create-job") return tiepNhanXe();
        if (thaoTac === "customer-vehicles")
            return xemXeCuaKhach(cacDongHienTai[Number(maBanGhi)], phanTuDich);
        if (thaoTac === "edit") return suaDanhMuc(Number(maBanGhi));
        if (thaoTac === "delete") {
            const cauHinhDanhMuc = cacCauHinhDanhMuc.find(
                    (giaTriTrangThai) => giaTriTrangThai.id === mucDangChon,
                ),
                dongDuLieu = cacDongHienTai[Number(maBanGhi)],
                maBanGhi = layKhoa(cauHinhDanhMuc, dongDuLieu);
            return xacNhan(
                "Xóa bản ghi",
                "Xóa " +
                    maBanGhi +
                    "? Bản ghi đã được dùng trong nghiệp vụ sẽ được bảo vệ bởi liên kết dữ liệu.",
                () =>
                    giaoTiep("/api/catalog/" + mucDangChon + "/" + encodeURIComponent(maBanGhi), {
                        method: "DELETE",
                    }),
            );
        }
        if (thaoTac === "job") return xemPhieu(maBanGhi);
        if (thaoTac === "accept") return nhanLich(maBanGhi);
        if (thaoTac === "reject")
            return xacNhan("Hủy lịch hẹn", "Hủy yêu cầu chưa tiếp nhận này?", () =>
                giaoTiep("/api/workshop/bookings/" + encodeURIComponent(maBanGhi) + "/reject", {
                    method: "POST",
                }),
            );
        if (thaoTac === "line-add") return themDong();
        if (thaoTac === "line-remove") {
            phanTuDich.closest(".line-row").remove();
            return;
        }
        if (thaoTac === "status") {
            const [phieuSua, trangThaiLoc] = maBanGhi.split("~");
            return xacNhan(
                "Cập nhật tiến độ",
                trangThaiLoc === "Đã hủy"
                    ? "Hủy phiếu và hoàn lại phụ tùng đã xuất?"
                    : "Chuyển phiếu sang “" + trangThaiLoc + "”?",
                () =>
                    giaoTiep("/api/workshop/jobs/" + phieuSua + "/status", {
                        method: "PUT",
                        body: JSON.stringify({ status: trangThaiLoc }),
                    }),
            );
        }
        if (thaoTac === "assign") {
            const cacLuaChon = await layLuaChon("mechanics");
            const cacTruong = [
                taoTruongChon("employeeId", "Thợ phụ trách", cacLuaChon),
                taoTruongNhap(
                    "start",
                    "Bắt đầu",
                    "datetime-local",
                    homNay() + "T08:00",
                    "required",
                ),
                taoTruongNhap(
                    "end",
                    "Kết thúc dự kiến",
                    "datetime-local",
                    homNay() + "T17:00",
                    "required",
                ),
            ].join("");
            return moHopThoai(
                "Phân công thợ",
                `<div class="form-grid">${cacTruong}</div>` +
                    `<p class="hint">Thợ chính tối đa 3 xe đang xử lý, thợ phụ tối đa 2 xe. Chọn lại cùng thợ để cập nhật thời gian.</p>`,
                async (duLieu) =>
                    (
                        await giaoTiep("/api/workshop/jobs/" + maBanGhi + "/assign", {
                            method: "POST",
                            body: JSON.stringify(Object.fromEntries(duLieu)),
                        })
                    ).message,
            );
        }
        if (thaoTac === "unassign") {
            const [phieuSua, nhanVien] = maBanGhi.split("~");
            return xacNhan("Bỏ phân công", "Bỏ thợ này khỏi phiếu đang chờ sửa?", () =>
                giaoTiep("/api/workshop/jobs/" + phieuSua + "/assign/" + nhanVien, {
                    method: "DELETE",
                }),
            );
        }
        if (thaoTac === "due-date")
            return moHopThoai(
                "Cập nhật ngày hẹn",
                taoTruongNhap(
                    "dueDate",
                    "Ngày hẹn trả",
                    "date",
                    homNay(),
                    `required min="${homNay()}"`,
                ),
                async (duLieu) =>
                    (
                        await giaoTiep("/api/workshop/jobs/" + maBanGhi + "/due-date", {
                            method: "PUT",
                            body: JSON.stringify(Object.fromEntries(duLieu)),
                        })
                    ).message,
            );
        if (thaoTac === "payment") {
            const duLieu = await giaoTiep("/api/workshop/jobs/" + maBanGhi);
            const ngayLapPhieu = String(duLieu.job.createdOn).slice(0, 10);
            const noiDungForm = [
                `<p>Khách hàng: <b>${maHoaHtml(duLieu.job.customerName)}</b></p>`,
                `<p>Tổng tiền cần thu: <b>${dinhDangTien(duLieu.job.total)}</b></p>`,
                taoTruongNhap(
                    "paidOn",
                    "Ngày thanh toán",
                    "date",
                    homNay(),
                    `required max="${homNay()}" min="${ngayLapPhieu}"`,
                ),
                `<p class="hint">Xác nhận khi đã nhận đủ tiền. Tổng tiền lấy từ chi tiết phiếu, không nhập lại thủ công.</p>`,
            ].join("");
            return moHopThoai(
                "Thu tiền phiếu " + maBanGhi,
                noiDungForm,
                async (truongNhap) =>
                    (
                        await giaoTiep("/api/workshop/jobs/" + maBanGhi + "/invoice", {
                            method: "POST",
                            body: JSON.stringify(Object.fromEntries(truongNhap)),
                        })
                    ).message,
                "Xác nhận đã thu tiền",
            );
        }
        if (thaoTac === "print-invoice") return xemPhieu(maBanGhi, true);
        if (thaoTac === "print") return window.print();
        if (thaoTac === "purchase-detail") return xemPhieuNhap(maBanGhi);
        if (thaoTac === "stock-adjust") return kiemKeTon();
        if (thaoTac === "password")
            return moHopThoai(
                "Đổi mật khẩu",
                `<div class="form-grid">` +
                    taoTruongNhap(
                        "oldPassword",
                        "Mật khẩu hiện tại",
                        "password",
                        "",
                        'required autocomplete="current-password"',
                    ) +
                    taoTruongNhap(
                        "newPassword",
                        "Mật khẩu mới",
                        "password",
                        "",
                        'required minlength="6" maxlength="100" autocomplete="new-password"',
                    ) +
                    `</div>`,
                async (duLieu) =>
                    (
                        await giaoTiep("/api/auth/password", {
                            method: "PUT",
                            body: JSON.stringify(Object.fromEntries(duLieu)),
                        })
                    ).message,
            );
        if (thaoTac === "content") return suaNoiDungDichVu(maBanGhi);
        if (["bold", "italic", "list"].includes(thaoTac)) {
            document.querySelector("#rich-editor").focus();
            document.execCommand(thaoTac === "list" ? "insertUnorderedList" : thaoTac);
        }
    }
    document.addEventListener("click", async (suKienNhap) => {
        const phanTuDich = suKienNhap.target.closest("[data-action]");
        if (!phanTuDich || phanTuDich.disabled) return;
        try {
            await xuLyThaoTac(phanTuDich.dataset.action, phanTuDich.dataset.id, phanTuDich);
        } catch (loiXuLy) {
            if (hopThoai.open) loi.textContent = loiXuLy.message;
            else hienThongBao(loiXuLy.message);
        }
    });
    function chuyenTrang() {
        if (location.hash === "#vehicles") history.replaceState(history.state, "", "#customers");
        const mucTiepTheo = location.hash.slice(1) || "dashboard";
        if (mucTiepTheo !== mucDangChon) {
            trang = 1;
            tuKhoa = "";
            tuNgay = "";
            denNgay = "";
            trangThaiLoc = "";
        }
        mucDangChon = mucTiepTheo;
        document.querySelector("#sidebar").classList.remove("open");
        taiDuLieu();
    }
    window.addEventListener("hashchange", chuyenTrang);
    document.querySelector("#today").textContent = new Intl.DateTimeFormat("vi-VN", {
        dateStyle: "full",
    }).format(new Date());
    giaoTiep("/api/catalog/schema")
        .then((duLieuTraVe) => {
            cacCauHinhDanhMuc = duLieuTraVe;
            chuyenTrang();
        })
        .catch((suKienNhap) => {
            noiDungTrang.innerHTML = `<div class="error-box">${maHoaHtml(suKienNhap.message)} <a href="/Account/Login">Đăng nhập lại</a></div>`;
        });
})();
