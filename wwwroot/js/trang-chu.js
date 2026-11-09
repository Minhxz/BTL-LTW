(() => {
    "use strict";
    window.TienIchSuaXe = {
        guiYeuCau: window.GiaoTiepMayChu.guiYeuCau,
        maHoaHtml(giaTriNhap) {
            return String(giaTriNhap ?? "").replace(
                /[&<>"']/g,
                (kyTuHtml) =>
                    ({
                        "&": "&amp;",
                        "<": "&lt;",
                        ">": "&gt;",
                        '"': "&quot;",
                        "'": "&#39;",
                    })[kyTuHtml],
            );
        },
        dinhDangNgay(giaTriNhap) {
            const cacPhuTung = String(giaTriNhap || "").split("-");
            return cacPhuTung.length === 3 ? cacPhuTung.reverse().join("/") : giaTriNhap;
        },
        homNay() {
            return new Intl.DateTimeFormat("sv-SE", {
                timeZone: "Asia/Bangkok",
            }).format(new Date());
        },
    };
    document.querySelector("#customer-logout")?.addEventListener("click", async (suKien) => {
        suKien.currentTarget.disabled = true;
        try {
            await window.TienIchSuaXe.guiYeuCau("/api/auth/logout", {
                method: "POST",
            });
            location.assign("/");
        } catch (loi) {
            suKien.currentTarget.disabled = false;
            alert(loi.message);
        }
    });
    const nutMoMenu = document.querySelector(".menu-toggle");
    const trinhDon = document.getElementById("main-nav");
    if (nutMoMenu && trinhDon) {
        const dongMenu = () => {
            trinhDon.classList.remove("open");
            nutMoMenu.setAttribute("aria-expanded", "false");
            nutMoMenu.setAttribute("aria-label", "Mở menu");
        };
        nutMoMenu.addEventListener("click", () => {
            const dangMo = trinhDon.classList.toggle("open");
            nutMoMenu.setAttribute("aria-expanded", String(dangMo));
            nutMoMenu.setAttribute("aria-label", dangMo ? "Đóng menu" : "Mở menu");
        });
        document.addEventListener("keydown", (suKien) => {
            if (suKien.key === "Escape") dongMenu();
        });
        trinhDon
            .querySelectorAll("a")
            .forEach((lienKet) => lienKet.addEventListener("click", dongMenu));
        window.addEventListener("resize", () => {
            if (window.innerWidth > 720) dongMenu();
        });
        trinhDon.querySelectorAll("a").forEach((lienKet) => {
            if (lienKet.pathname.toLowerCase() === location.pathname.toLowerCase())
                lienKet.setAttribute("aria-current", "page");
        });
    }
})();
