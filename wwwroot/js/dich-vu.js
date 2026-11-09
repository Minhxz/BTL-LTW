(() => {
    "use strict";
    const danhMuc = document.getElementById("service-catalog");
    if (!danhMuc) return;
    const hopThoai = document.getElementById("service-dialog");
    const thongBao = document.getElementById("service-catalog-message");
    const nutThuLai = document.getElementById("service-catalog-retry");
    const dinhDangTien = (giaTriNhap) =>
        new Intl.NumberFormat("vi-VN", {
            style: "currency",
            currency: "VND",
            maximumFractionDigits: 0,
        }).format(Number(giaTriNhap) || 0);
    let nutMo;
    function layAnhHopLe(giaTriNhap) {
        if (!giaTriNhap) return "";
        try {
            const duongDan = new URL(giaTriNhap, location.origin);
            return duongDan.origin === location.origin && duongDan.pathname.startsWith("/")
                ? duongDan.href
                : "";
        } catch {
            return "";
        }
    }
    function taoMoTa(giaTriNhap) {
        const nguon = new DOMParser().parseFromString(String(giaTriNhap || ""), "text/html");
        const cacTheChoPhep = new Set(["P", "BR", "STRONG", "EM", "UL", "OL", "LI"]);
        const cacTheBiChan = new Set([
            "SCRIPT",
            "STYLE",
            "IFRAME",
            "OBJECT",
            "SVG",
            "MATH",
            "TEMPLATE",
        ]);
        function saoChep(nutTaiLieu) {
            if (nutTaiLieu.nodeType === Node.TEXT_NODE)
                return document.createTextNode(nutTaiLieu.textContent);
            const doanTaiLieu = document.createDocumentFragment();
            if (nutTaiLieu.nodeType !== Node.ELEMENT_NODE || cacTheBiChan.has(nutTaiLieu.tagName))
                return doanTaiLieu;
            const tenThe =
                { B: "STRONG", I: "EM", DIV: "P" }[nutTaiLieu.tagName] || nutTaiLieu.tagName;
            const phanTuDich = cacTheChoPhep.has(tenThe)
                ? document.createElement(tenThe.toLowerCase())
                : doanTaiLieu;
            nutTaiLieu.childNodes.forEach((phanTuCon) => phanTuDich.append(saoChep(phanTuCon)));
            return phanTuDich;
        }
        const ketQua = document.createDocumentFragment();
        nguon.body.childNodes.forEach((phanTuCon) => ketQua.append(saoChep(phanTuCon)));
        return ketQua;
    }
    function hienDichVu(muc, nut) {
        nutMo = nut;
        document.getElementById("service-dialog-title").textContent = muc.name;
        document.getElementById("service-dialog-price").textContent =
            "Giá tham khảo: " + dinhDangTien(muc.price);
        document
            .getElementById("service-dialog-description")
            .replaceChildren(taoMoTa(muc.description));
        const anh = document.getElementById("service-dialog-image");
        anh.hidden = true;
        anh.onerror = () => {
            anh.hidden = true;
        };
        anh.onload = () => {
            anh.hidden = false;
        };
        anh.alt = muc.name;
        const nguon = layAnhHopLe(muc.imageUrl);
        if (nguon) anh.src = nguon;
        else anh.removeAttribute("src");
        document.getElementById("service-dialog-booking").href =
            "/Home/Booking?service=" + encodeURIComponent(muc.id);
        hopThoai.showModal();
    }
    function theNoiDung(muc) {
        const baiViet = document.createElement("article");
        baiViet.className = "service-card catalog-card";
        const anhDaiDien = document.createElement("div");
        anhDaiDien.className = "catalog-photo";
        const giaTriDuPhong = document.createElement("span");
        giaTriDuPhong.className = "catalog-photo-label";
        giaTriDuPhong.textContent = "CỬA HÀNG SỬA XE";
        anhDaiDien.append(giaTriDuPhong);
        const nguon = layAnhHopLe(muc.imageUrl);
        if (nguon) {
            const anh = document.createElement("img");
            anh.src = nguon;
            anh.alt = muc.name;
            anh.loading = "lazy";
            anh.addEventListener("error", () => anh.remove());
            anhDaiDien.append(anh);
        }
        const tieuDeNhap = document.createElement("h3");
        tieuDeNhap.textContent = muc.name;
        const donGia = document.createElement("p");
        donGia.className = "catalog-price";
        donGia.textContent = "Tham khảo: " + dinhDangTien(muc.price);
        const moTaNgan = document.createElement("p");
        moTaNgan.className = "catalog-excerpt";
        const vanBan = taoMoTa(muc.description).textContent.trim();
        moTaNgan.textContent =
            vanBan.length > 145
                ? vanBan.slice(0, 145) + "…"
                : vanBan || "Xem chi tiết dịch vụ và đặt lịch theo nhu cầu của bạn.";
        const nut = document.createElement("button");
        nut.type = "button";
        nut.textContent = "XEM CHI TIẾT ↗";
        nut.addEventListener("click", () => hienDichVu(muc, nut));
        baiViet.append(anhDaiDien, tieuDeNhap, donGia, moTaNgan, nut);
        return baiViet;
    }
    async function taiDanhSach() {
        nutThuLai.hidden = true;
        thongBao.hidden = false;
        thongBao.textContent = "Đang tải dịch vụ…";
        try {
            const duLieuTraVe = await window.TienIchSuaXe.guiYeuCau("/api/services");
            const cacMucDuLieu = duLieuTraVe.services || [];
            danhMuc.replaceChildren(...cacMucDuLieu.map(theNoiDung));
            thongBao.textContent = cacMucDuLieu.length
                ? ""
                : "Chưa có dịch vụ. Vui lòng quay lại sau.";
            thongBao.hidden = Boolean(cacMucDuLieu.length);
        } catch (loi) {
            thongBao.textContent = loi.message;
            nutThuLai.hidden = false;
        }
    }
    nutThuLai.addEventListener("click", taiDanhSach);
    hopThoai.querySelector("[data-close-dialog]").addEventListener("click", () => hopThoai.close());
    hopThoai.addEventListener("click", (suKien) => {
        if (suKien.target !== hopThoai) return;
        const khung = hopThoai.getBoundingClientRect();
        if (
            suKien.clientX < khung.left ||
            suKien.clientX > khung.right ||
            suKien.clientY < khung.top ||
            suKien.clientY > khung.bottom
        )
            hopThoai.close();
    });
    hopThoai.addEventListener("close", () => nutMo?.focus());
    taiDanhSach();
})();
