(() => {
    "use strict";
    const bieuMau = document.getElementById("tracking-form");
    if (!bieuMau) return;
    const giaoTiep = window.TienIchSuaXe;
    const ketQua = document.getElementById("tracking-result");
    const thongBao = document.getElementById("tracking-message");
    const vungTrong = document.getElementById("tracking-empty");
    let dangBan = false;
    const maTraCuu = new URLSearchParams(location.search).get("reference");
    if (maTraCuu) bieuMau.elements.reference.value = maTraCuu.slice(0, 40);
    function taoPhanTu(tenThe, noiDung, tenLop) {
        const nutTaiLieu = document.createElement(tenThe);
        if (noiDung !== undefined) nutTaiLieu.textContent = noiDung;
        if (tenLop) nutTaiLieu.className = tenLop;
        return nutTaiLieu;
    }
    function datTrangThaiBan(giaTriNhap) {
        dangBan = giaTriNhap;
        bieuMau.querySelectorAll("input, button").forEach((oDieuKhien) => {
            oDieuKhien.disabled = giaTriNhap;
        });
    }
    function hienLoiNhan(noiDung, thanhCong = false) {
        thongBao.textContent = noiDung;
        thongBao.classList.toggle("success", thanhCong);
        thongBao.hidden = !noiDung;
    }
    function hienKetQua(muc, soDienThoai) {
        const phanDau = taoPhanTu("div", undefined, "tracking-result-head");
        const tieuDeChinh = taoPhanTu("div");
        tieuDeChinh.append(
            taoPhanTu("span", "MÃ LỊCH HẸN · " + muc.reference, "eyebrow"),
            taoPhanTu("h3", "Tiến độ lịch hẹn"),
        );
        phanDau.append(tieuDeChinh, taoPhanTu("span", muc.statusLabel, "status-badge"));
        const chiTiet = taoPhanTu("div", undefined, "tracking-details");
        [
            ["Mẫu xe", muc.vehicleType],
            ["Hãng xe", muc.brand],
            ["Biển số", muc.licensePlate || "Chưa cập nhật"],
            ["Dịch vụ", muc.service],
            ["Thời gian hẹn", giaoTiep.dinhDangNgay(muc.date) + " · " + muc.time],
            ["Khách hàng", muc.customerName],
        ].forEach(([nhanHienThi, giaTriNhap]) => {
            const oDuLieu = taoPhanTu("div");
            oDuLieu.append(taoPhanTu("small", nhanHienThi), taoPhanTu("strong", giaTriNhap));
            chiTiet.append(oDuLieu);
        });
        const cacBuoc = taoPhanTu("ol", undefined, "tracking-stages");
        (muc.stages || []).forEach((buoc, viTri) => {
            const trangThaiMuc = ["completed", "current", "upcoming"].includes(buoc.state)
                ? buoc.state
                : "upcoming";
            const nutTaiLieu = taoPhanTu("li", undefined, trangThaiMuc);
            nutTaiLieu.append(
                taoPhanTu("span", trangThaiMuc === "completed" ? "✓" : viTri + 1),
                taoPhanTu("strong", buoc.title),
                taoPhanTu("p", buoc.description),
            );
            cacBuoc.append(nutTaiLieu);
        });
        ketQua.replaceChildren(phanDau, chiTiet, cacBuoc);
        if (muc.processingNote?.trim()) {
            const ghiChuNhap = taoPhanTu("section", undefined, "tracking-processing-note");
            ghiChuNhap.append(
                taoPhanTu("h4", "Ghi chú từ Cửa Hàng Sửa Xe"),
                taoPhanTu("p", muc.processingNote),
            );
            ketQua.append(ghiChuNhap);
        }
        if (muc.canCancel === true) {
            const nutHuy = taoPhanTu("button", "Hủy lịch hẹn này", "cancel-booking");
            nutHuy.type = "button";
            nutHuy.addEventListener("click", () => {
                if (dangBan) return;
                nutHuy.hidden = true;
                const xacNhanThaoTac = taoPhanTu("div", undefined, "cancel-confirmation");
                const nutDongY = taoPhanTu("button", "Xác nhận hủy");
                nutDongY.type = "button";
                const nutKhong = taoPhanTu("button", "Giữ lịch hẹn");
                nutKhong.type = "button";
                xacNhanThaoTac.append(
                    taoPhanTu("p", "Bạn muốn hủy lịch hẹn này?"),
                    nutDongY,
                    nutKhong,
                );
                nutKhong.addEventListener("click", () => {
                    if (!dangBan) {
                        xacNhanThaoTac.remove();
                        nutHuy.hidden = false;
                        nutHuy.focus();
                    }
                });
                nutDongY.addEventListener("click", async () => {
                    if (dangBan) return;
                    datTrangThaiBan(true);
                    nutDongY.disabled = true;
                    nutKhong.disabled = true;
                    hienLoiNhan("");
                    try {
                        const daCapNhat = await giaoTiep.guiYeuCau(
                            "/api/appointments/" + encodeURIComponent(muc.reference) + "/cancel",
                            { method: "POST", body: JSON.stringify({ phone: soDienThoai }) },
                        );
                        hienKetQua(daCapNhat, soDienThoai);
                        hienLoiNhan("Lịch hẹn đã được hủy.", true);
                    } catch (loi) {
                        hienLoiNhan(loi.message);
                        nutDongY.disabled = false;
                        nutKhong.disabled = false;
                    } finally {
                        datTrangThaiBan(false);
                    }
                });
                ketQua.append(xacNhanThaoTac);
                nutDongY.focus();
            });
            ketQua.append(nutHuy);
        }
        ketQua.hidden = false;
        vungTrong.hidden = true;
    }
    bieuMau.addEventListener("submit", async (suKien) => {
        suKien.preventDefault();
        if (dangBan || !bieuMau.reportValidity()) return;
        const soDienThoai = bieuMau.elements.phone.value.trim();
        const thamSo = new URLSearchParams({
            reference: bieuMau.elements.reference.value.trim(),
            phone: soDienThoai,
        });
        datTrangThaiBan(true);
        hienLoiNhan("");
        ketQua.hidden = true;
        vungTrong.hidden = true;
        try {
            hienKetQua(
                await giaoTiep.guiYeuCau("/api/repairs/lookup?" + thamSo.toString()),
                soDienThoai,
            );
        } catch (loi) {
            hienLoiNhan(loi.message);
            vungTrong.hidden = false;
        } finally {
            datTrangThaiBan(false);
        }
    });
})();
