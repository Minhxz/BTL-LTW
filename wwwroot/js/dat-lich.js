(() => {
    "use strict";
    const bieuMau = document.getElementById("booking-form");
    if (!bieuMau) return;
    const giaoTiep = window.TienIchSuaXe;
    const dinhDangNgay = bieuMau.elements.date;
    const gio = bieuMau.elements.time;
    const dichVu = bieuMau.elements.service;
    const thongBao = document.getElementById("booking-message");
    const nutGui = document.getElementById("booking-submit");
    const thanhCong = document.getElementById("booking-success");
    const nutLuuBanNhap = document.getElementById("booking-save-draft");
    const nutKhoiPhucBanNhap = document.getElementById("booking-restore-draft");
    const nutXoaBanNhap = document.getElementById("booking-clear-draft");
    const thongBaoBanNhap = document.getElementById("booking-draft-message");
    const nutTaiLaiDichVu = document.getElementById("booking-services-retry");
    const cacTruong = [
        "customerName",
        "phone",
        "vehicleType",
        "brand",
        "licensePlate",
        "service",
        "date",
        "time",
        "note",
    ];
    const thamSoTruyVan = new URLSearchParams(location.search);
    let daTaiDichVu = false,
        dangBan = false,
        coBanNhap = false,
        phienBanBanNhap = 0;
    const homNay = giaoTiep.homNay();
    dinhDangNgay.min = homNay;
    const gioiHanToiDa = new Date(homNay + "T12:00:00+07:00");
    gioiHanToiDa.setDate(gioiHanToiDa.getDate() + 90);
    dinhDangNgay.max = new Intl.DateTimeFormat("sv-SE", {
        timeZone: "Asia/Bangkok",
    }).format(gioiHanToiDa);
    function capNhatGioHen() {
        const bayGio = new Intl.DateTimeFormat("en-GB", {
            timeZone: "Asia/Bangkok",
            hour: "2-digit",
            minute: "2-digit",
            hour12: false,
        }).format(new Date());
        [...gio.options].forEach((luaChon) => {
            luaChon.disabled = Boolean(
                luaChon.value &&
                dinhDangNgay.value === giaoTiep.homNay() &&
                luaChon.value <= bayGio,
            );
        });
        if (gio.selectedOptions[0]?.disabled) gio.value = "";
    }
    dinhDangNgay.addEventListener("change", capNhatGioHen);
    function capNhatThaoTac() {
        nutLuuBanNhap.disabled = dangBan;
        nutKhoiPhucBanNhap.disabled = dangBan || !coBanNhap || !daTaiDichVu;
        nutXoaBanNhap.hidden = !coBanNhap;
        nutXoaBanNhap.disabled = dangBan;
        nutGui.disabled = dangBan || !daTaiDichVu;
        dichVu.disabled = dangBan || !daTaiDichVu;
    }
    function datTrangThaiBan(giaTriNhap) {
        dangBan = giaTriNhap;
        bieuMau.querySelectorAll("input, select, textarea").forEach((duLieuNhap) => {
            duLieuNhap.disabled = giaTriNhap;
        });
        nutTaiLaiDichVu.disabled = giaTriNhap;
        capNhatThaoTac();
    }
    function trangThaiBanNhap(noiDung, loi = false) {
        thongBaoBanNhap.textContent = noiDung;
        thongBaoBanNhap.classList.toggle("draft-error", loi);
    }
    function cacGiaTri() {
        return Object.fromEntries(
            cacTruong.map((ten) => [ten, bieuMau.elements[ten].value.trim()]),
        );
    }
    async function taiDichVu() {
        nutTaiLaiDichVu.hidden = true;
        dichVu.disabled = true;
        try {
            const duLieuTraVe = await giaoTiep.guiYeuCau("/api/services");
            const cacMucDuLieu = duLieuTraVe.services || [];
            dichVu.replaceChildren(
                new Option(cacMucDuLieu.length ? "Chọn dịch vụ" : "Chưa có dịch vụ", ""),
                ...cacMucDuLieu.map((muc) => new Option(muc.name, muc.id)),
            );
            daTaiDichVu = Boolean(cacMucDuLieu.length);
            const daChon = thamSoTruyVan.get("service");
            if ([...dichVu.options].some((luaChon) => luaChon.value === daChon))
                dichVu.value = daChon;
            if (!cacMucDuLieu.length) {
                thongBao.textContent = "Hiện chưa có dịch vụ để đặt lịch. Vui lòng quay lại sau.";
                thongBao.hidden = false;
            } else {
                thongBao.hidden = true;
            }
        } catch (loi) {
            dichVu.replaceChildren(new Option("Không tải được dịch vụ", ""));
            thongBao.textContent = loi.message;
            thongBao.hidden = false;
            nutTaiLaiDichVu.hidden = false;
        } finally {
            capNhatThaoTac();
        }
    }
    nutTaiLaiDichVu.addEventListener("click", taiDichVu);
    nutLuuBanNhap.addEventListener("click", async () => {
        if (dangBan) return;
        const banNhap = cacGiaTri();
        phienBanBanNhap++;
        datTrangThaiBan(true);
        try {
            await giaoTiep.guiYeuCau("/api/appointments/draft", {
                method: "PUT",
                body: JSON.stringify(banNhap),
            });
            coBanNhap = true;
            trangThaiBanNhap("Đã lưu bản nháp trong phiên này. Bạn có thể khôi phục khi quay lại.");
        } catch (loi) {
            trangThaiBanNhap(loi.message, true);
        } finally {
            datTrangThaiBan(false);
        }
    });
    nutKhoiPhucBanNhap.addEventListener("click", async () => {
        if (dangBan || !daTaiDichVu) return;
        phienBanBanNhap++;
        datTrangThaiBan(true);
        try {
            const duLieuTraVe = await giaoTiep.guiYeuCau("/api/appointments/draft");
            coBanNhap = Boolean(duLieuTraVe.draft);
            if (!coBanNhap) {
                trangThaiBanNhap("Không còn bản nháp trong phiên này.");
                return;
            }
            cacTruong.forEach((ten) => {
                bieuMau.elements[ten].value =
                    typeof duLieuTraVe.draft[ten] === "string" ? duLieuTraVe.draft[ten] : "";
            });
            const loaiXeCu = [
                "Ô tô",
                "Xe máy",
                "Xe điện & hybrid",
                "Xe bán tải / xe tải nhẹ",
            ].includes(duLieuTraVe.draft.vehicleType);
            if (loaiXeCu) {
                bieuMau.elements.vehicleType.value = "";
                bieuMau.elements.brand.value = "";
            }
            capNhatGioHen();
            trangThaiBanNhap(
                loaiXeCu
                    ? "Đã khôi phục bản nháp cũ. Vui lòng nhập lại mẫu xe và hãng xe."
                    : "Đã khôi phục bản nháp. Kiểm tra lại dịch vụ, ngày và giờ trước khi gửi.",
            );
        } catch (loi) {
            trangThaiBanNhap(loi.message, true);
        } finally {
            datTrangThaiBan(false);
        }
    });
    nutXoaBanNhap.addEventListener("click", async () => {
        if (dangBan) return;
        phienBanBanNhap++;
        datTrangThaiBan(true);
        try {
            await giaoTiep.guiYeuCau("/api/appointments/draft", { method: "DELETE" });
            coBanNhap = false;
            trangThaiBanNhap("Đã xóa bản nháp. Thông tin đang nhập vẫn được giữ trên form.");
        } catch (loi) {
            trangThaiBanNhap(loi.message, true);
        } finally {
            datTrangThaiBan(false);
        }
    });
    bieuMau.addEventListener("submit", async (suKien) => {
        suKien.preventDefault();
        if (dangBan || !daTaiDichVu) return;
        capNhatGioHen();
        if (!bieuMau.reportValidity()) return;
        const lichHen = cacGiaTri();
        phienBanBanNhap++;
        datTrangThaiBan(true);
        thongBao.hidden = true;
        nutGui.textContent = "ĐANG GỬI YÊU CẦU…";
        try {
            const ketQua = await giaoTiep.guiYeuCau("/api/appointments", {
                method: "POST",
                body: JSON.stringify(lichHen),
            });
            document.getElementById("success-reference").textContent = ketQua.reference;
            document.getElementById("success-summary").textContent =
                giaoTiep.dinhDangNgay(ketQua.date) + " · " + ketQua.time + " · " + ketQua.service;
            document.getElementById("success-tracking").href =
                "/Home/Tracking?reference=" + encodeURIComponent(ketQua.reference);
            try {
                await giaoTiep.guiYeuCau("/api/appointments/draft", { method: "DELETE" });
                coBanNhap = false;
                trangThaiBanNhap("Bản nháp được lưu tạm trong phiên trình duyệt này.");
            } catch {
                trangThaiBanNhap(
                    "Lịch hẹn đã gửi thành công. Bản nháp cũ chưa được xóa; bạn có thể xóa khi đặt lịch khác.",
                    true,
                );
            }
            bieuMau.hidden = true;
            thanhCong.hidden = false;
            thanhCong.scrollIntoView({ behavior: "smooth", block: "center" });
        } catch (loi) {
            thongBao.classList.remove("success");
            thongBao.textContent = loi.message;
            thongBao.hidden = false;
            const taoTruongNhap = Object.keys(loi.fields || {})[0];
            const duLieuNhap = [...bieuMau.elements].find(
                (muc) => muc.name?.toLowerCase() === taoTruongNhap?.toLowerCase(),
            );
            datTrangThaiBan(false);
            duLieuNhap?.focus();
        } finally {
            datTrangThaiBan(false);
            nutGui.textContent = "GỬI YÊU CẦU ĐẶT LỊCH ↗";
        }
    });
    document.getElementById("copy-reference").addEventListener("click", async (suKien) => {
        try {
            await navigator.clipboard.writeText(
                document.getElementById("success-reference").textContent,
            );
            suKien.target.textContent = "Đã sao chép";
        } catch {
            suKien.target.textContent = "Chọn mã để sao chép";
            const luaChonHienTai = window.getSelection();
            const vungChon = document.createRange();
            vungChon.selectNodeContents(document.getElementById("success-reference"));
            luaChonHienTai.removeAllRanges();
            luaChonHienTai.addRange(vungChon);
        }
    });
    document.getElementById("new-booking").addEventListener("click", () => {
        thanhCong.hidden = true;
        bieuMau.hidden = false;
        bieuMau.reset();
        thongBao.hidden = true;
        capNhatGioHen();
        capNhatThaoTac();
        document.getElementById("copy-reference").textContent = "Sao chép mã";
        bieuMau.elements.customerName.focus();
    });
    async function kiemTraBanNhap() {
        const phienBan = phienBanBanNhap;
        try {
            const duLieuTraVe = await giaoTiep.guiYeuCau("/api/appointments/draft");
            if (phienBan !== phienBanBanNhap) return;
            coBanNhap = Boolean(duLieuTraVe.draft);
            if (coBanNhap)
                trangThaiBanNhap(
                    "Có bản nháp trong phiên này. Chọn “Khôi phục bản nháp” để tiếp tục.",
                );
        } catch {
            if (phienBan === phienBanBanNhap)
                trangThaiBanNhap(
                    "Chưa tải được bản nháp. Bạn vẫn có thể điền và gửi lịch hẹn.",
                    true,
                );
        } finally {
            capNhatThaoTac();
        }
    }
    taiDichVu();
    kiemTraBanNhap();
})();
