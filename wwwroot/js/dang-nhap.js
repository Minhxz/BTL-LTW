(() => {
    "use strict";
    const bieuMau = document.querySelector("#login-form");
    const oMatKhau = document.querySelector("#password");
    document.querySelector("#password-toggle").addEventListener("click", (suKien) => {
        const dangHien = oMatKhau.type === "password";
        oMatKhau.type = dangHien ? "text" : "password";
        suKien.currentTarget.textContent = dangHien ? "Ẩn" : "Hiện";
        suKien.currentTarget.setAttribute("aria-label", dangHien ? "Ẩn mật khẩu" : "Hiện mật khẩu");
    });
    bieuMau.addEventListener("submit", async (suKien) => {
        suKien.preventDefault();
        const nut = document.querySelector("#login-button");
        const thongBao = document.querySelector("#login-message");
        nut.disabled = true;
        thongBao.textContent = "";
        try {
            const ketQua = await window.GiaoTiepMayChu.guiYeuCau("/api/auth/login", {
                method: "POST",
                body: JSON.stringify({
                    username: document.querySelector("#username").value.trim(),
                    password: oMatKhau.value,
                }),
            });
            const duongDanQuayLai = bieuMau.dataset.returnUrl;
            window.location.assign(
                ketQua.role === "Manager" &&
                    duongDanQuayLai &&
                    duongDanQuayLai.startsWith("/") &&
                    !duongDanQuayLai.startsWith("//")
                    ? duongDanQuayLai
                    : ketQua.redirectUrl,
            );
        } catch (loi) {
            thongBao.textContent = loi.message || "Không thể kết nối. Vui lòng thử lại.";
            nut.disabled = false;
        }
    });
})();
