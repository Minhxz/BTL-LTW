(() => {
    "use strict";
    let maChongGiaMao;
    async function layMaChongGiaMao() {
        maChongGiaMao ??= fetch("/api/auth/csrf", {
            credentials: "same-origin",
            cache: "no-store",
        })
            .then(async (phanHoi) => {
                if (!phanHoi.ok)
                    throw new Error("Không thể khởi tạo phiên làm việc. Vui lòng tải lại trang.");
                return (await phanHoi.json()).token;
            })
            .catch((loi) => {
                maChongGiaMao = null;
                throw loi;
            });
        return maChongGiaMao;
    }
    async function guiYeuCau(duongDan, layLuaChon = {}) {
        if (new URL(duongDan, location.href).origin !== location.origin)
            throw new Error("Yêu cầu phải được gửi trong website Cửa Hàng Sửa Xe.");
        const phuongThuc = (layLuaChon.method || "GET").toUpperCase();
        const tieuDeYeuCau = new Headers(layLuaChon.headers);
        if (layLuaChon.body != null && !(layLuaChon.body instanceof FormData))
            tieuDeYeuCau.set("Content-Type", "application/json");
        if (!["GET", "HEAD", "OPTIONS"].includes(phuongThuc))
            tieuDeYeuCau.set("X-CSRF-TOKEN", await layMaChongGiaMao());
        let phanHoi;
        try {
            phanHoi = await fetch(duongDan, {
                ...layLuaChon,
                method: phuongThuc,
                headers: tieuDeYeuCau,
                credentials: "same-origin",
                cache: "no-store",
            });
        } catch {
            throw new Error("Không thể kết nối tới máy chủ. Vui lòng thử lại.");
        }
        const duLieuTraVe = phanHoi.status === 204 ? {} : await phanHoi.json().catch(() => ({}));
        if (!phanHoi.ok) {
            const chiTiet = Object.values(duLieuTraVe.errors || {})
                .flat()
                .slice(0, 3)
                .join(" ");
            const loi = new Error(
                chiTiet ||
                    duLieuTraVe.message ||
                    "Không thể xử lý yêu cầu. Vui lòng tải lại trang và thử lại.",
            );
            loi.fields = duLieuTraVe.errors || {};
            loi.status = phanHoi.status;
            throw loi;
        }
        return duLieuTraVe;
    }
    window.GiaoTiepMayChu = { guiYeuCau };
})();
