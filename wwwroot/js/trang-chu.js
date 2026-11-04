(() => {
  "use strict";
  window.TienIchSuaXe = {
    request: window.GiaoTiepMayChu.request,
    escape(value) {
      return String(value ?? "").replace(
        /[&<>"']/g,
        (ch) =>
          ({
            "&": "&amp;",
            "<": "&lt;",
            ">": "&gt;",
            '"': "&quot;",
            "'": "&#39;",
          })[ch],
      );
    },
    date(value) {
      const parts = String(value || "").split("-");
      return parts.length === 3 ? parts.reverse().join("/") : value;
    },
    today() {
      return new Intl.DateTimeFormat("sv-SE", {
        timeZone: "Asia/Bangkok",
      }).format(new Date());
    },
  };
  document
    .querySelector("#customer-logout")
    ?.addEventListener("click", async (event) => {
      event.currentTarget.disabled = true;
      try {
        await window.TienIchSuaXe.request("/api/auth/logout", {
          method: "POST",
        });
        location.assign("/");
      } catch (error) {
        event.currentTarget.disabled = false;
        alert(error.message);
      }
    });
  const toggle = document.querySelector(".menu-toggle");
  const menu = document.getElementById("main-nav");
  if (toggle && menu) {
    const close = () => {
      menu.classList.remove("open");
      toggle.setAttribute("aria-expanded", "false");
      toggle.setAttribute("aria-label", "Mở menu");
    };
    toggle.addEventListener("click", () => {
      const open = menu.classList.toggle("open");
      toggle.setAttribute("aria-expanded", String(open));
      toggle.setAttribute("aria-label", open ? "Đóng menu" : "Mở menu");
    });
    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") close();
    });
    menu
      .querySelectorAll("a")
      .forEach((link) => link.addEventListener("click", close));
    window.addEventListener("resize", () => {
      if (window.innerWidth > 720) close();
    });
    menu.querySelectorAll("a").forEach((link) => {
      if (link.pathname.toLowerCase() === location.pathname.toLowerCase())
        link.setAttribute("aria-current", "page");
    });
  }
})();
