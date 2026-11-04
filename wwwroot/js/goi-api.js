(() => {
  "use strict";
  let csrf;
  async function token() {
    csrf ??= fetch("/api/auth/csrf", {
      credentials: "same-origin",
      cache: "no-store",
    })
      .then(async (response) => {
        if (!response.ok)
          throw new Error(
            "Không thể khởi tạo phiên làm việc. Vui lòng tải lại trang.",
          );
        return (await response.json()).token;
      })
      .catch((error) => {
        csrf = null;
        throw error;
      });
    return csrf;
  }
  async function request(url, options = {}) {
    if (new URL(url, location.href).origin !== location.origin)
      throw new Error("Yêu cầu phải được gửi trong website Cửa Hàng Sửa Xe.");
    const method = (options.method || "GET").toUpperCase();
    const headers = new Headers(options.headers);
    if (options.body != null && !(options.body instanceof FormData))
      headers.set("Content-Type", "application/json");
    if (!["GET", "HEAD", "OPTIONS"].includes(method))
      headers.set("X-CSRF-TOKEN", await token());
    let response;
    try {
      response = await fetch(url, {
        ...options,
        method,
        headers,
        credentials: "same-origin",
        cache: "no-store",
      });
    } catch {
      throw new Error("Không thể kết nối tới máy chủ. Vui lòng thử lại.");
    }
    const data =
      response.status === 204 ? {} : await response.json().catch(() => ({}));
    if (!response.ok) {
      const details = Object.values(data.errors || {})
        .flat()
        .slice(0, 3)
        .join(" ");
      const error = new Error(
        details ||
          data.message ||
          "Không thể xử lý yêu cầu. Vui lòng tải lại trang và thử lại.",
      );
      error.fields = data.errors || {};
      error.status = response.status;
      throw error;
    }
    return data;
  }
  window.GiaoTiepMayChu = { request };
})();
