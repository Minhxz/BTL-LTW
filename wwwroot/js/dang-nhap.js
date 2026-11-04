(() => {
  "use strict";
  const form = document.querySelector("#login-form");
  const password = document.querySelector("#password");
  document
    .querySelector("#password-toggle")
    .addEventListener("click", (event) => {
      const visible = password.type === "password";
      password.type = visible ? "text" : "password";
      event.currentTarget.textContent = visible ? "Ẩn" : "Hiện";
      event.currentTarget.setAttribute(
        "aria-label",
        visible ? "Ẩn mật khẩu" : "Hiện mật khẩu",
      );
    });
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    const button = document.querySelector("#login-button");
    const message = document.querySelector("#login-message");
    button.disabled = true;
    message.textContent = "";
    try {
      const result = await window.GiaoTiepMayChu.request("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({
          username: document.querySelector("#username").value.trim(),
          password: password.value,
        }),
      });
      const returnUrl = form.dataset.returnUrl;
      window.location.assign(
        result.role === "Manager" &&
          returnUrl &&
          returnUrl.startsWith("/") &&
          !returnUrl.startsWith("//")
          ? returnUrl
          : result.redirectUrl,
      );
    } catch (error) {
      message.textContent =
        error.message || "Không thể kết nối. Vui lòng thử lại.";
      button.disabled = false;
    }
  });
})();
