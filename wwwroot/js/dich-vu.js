(() => {
  "use strict";
  const catalog = document.getElementById("service-catalog");
  if (!catalog) return;
  const dialog = document.getElementById("service-dialog");
  const message = document.getElementById("service-catalog-message");
  const retry = document.getElementById("service-catalog-retry");
  const money = (value) =>
    new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: "VND",
      maximumFractionDigits: 0,
    }).format(Number(value) || 0);
  let trigger;
  function safeImage(value) {
    if (!value) return "";
    try {
      const url = new URL(value, location.origin);
      return url.origin === location.origin && url.pathname.startsWith("/")
        ? url.href
        : "";
    } catch {
      return "";
    }
  }
  function description(value) {
    const source = new DOMParser().parseFromString(
      String(value || ""),
      "text/html",
    );
    const allowed = new Set(["P", "BR", "STRONG", "EM", "UL", "OL", "LI"]);
    const blocked = new Set([
      "SCRIPT",
      "STYLE",
      "IFRAME",
      "OBJECT",
      "SVG",
      "MATH",
      "TEMPLATE",
    ]);
    function copy(node) {
      if (node.nodeType === Node.TEXT_NODE)
        return document.createTextNode(node.textContent);
      const fragment = document.createDocumentFragment();
      if (node.nodeType !== Node.ELEMENT_NODE || blocked.has(node.tagName))
        return fragment;
      const tag =
        { B: "STRONG", I: "EM", DIV: "P" }[node.tagName] || node.tagName;
      const target = allowed.has(tag)
        ? document.createElement(tag.toLowerCase())
        : fragment;
      node.childNodes.forEach((child) => target.append(copy(child)));
      return target;
    }
    const result = document.createDocumentFragment();
    source.body.childNodes.forEach((child) => result.append(copy(child)));
    return result;
  }
  function showService(item, button) {
    trigger = button;
    document.getElementById("service-dialog-title").textContent = item.name;
    document.getElementById("service-dialog-price").textContent =
      "Giá tham khảo: " + money(item.price);
    document
      .getElementById("service-dialog-description")
      .replaceChildren(description(item.description));
    const image = document.getElementById("service-dialog-image");
    image.hidden = true;
    image.onerror = () => {
      image.hidden = true;
    };
    image.onload = () => {
      image.hidden = false;
    };
    image.alt = item.name;
    const source = safeImage(item.imageUrl);
    if (source) image.src = source;
    else image.removeAttribute("src");
    document.getElementById("service-dialog-booking").href =
      "/Home/Booking?service=" + encodeURIComponent(item.id);
    dialog.showModal();
  }
  function card(item) {
    const article = document.createElement("article");
    article.className = "service-card catalog-card";
    const photo = document.createElement("div");
    photo.className = "catalog-photo";
    const fallback = document.createElement("span");
    fallback.className = "catalog-photo-label";
    fallback.textContent = "CỬA HÀNG SỬA XE";
    photo.append(fallback);
    const source = safeImage(item.imageUrl);
    if (source) {
      const image = document.createElement("img");
      image.src = source;
      image.alt = item.name;
      image.loading = "lazy";
      image.addEventListener("error", () => image.remove());
      photo.append(image);
    }
    const title = document.createElement("h3");
    title.textContent = item.name;
    const price = document.createElement("p");
    price.className = "catalog-price";
    price.textContent = "Tham khảo: " + money(item.price);
    const excerpt = document.createElement("p");
    excerpt.className = "catalog-excerpt";
    const plain = description(item.description).textContent.trim();
    excerpt.textContent =
      plain.length > 145
        ? plain.slice(0, 145) + "…"
        : plain || "Xem chi tiết dịch vụ và đặt lịch theo nhu cầu của bạn.";
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = "XEM CHI TIẾT ↗";
    button.addEventListener("click", () => showService(item, button));
    article.append(photo, title, price, excerpt, button);
    return article;
  }
  async function load() {
    retry.hidden = true;
    message.hidden = false;
    message.textContent = "Đang tải dịch vụ…";
    try {
      const data = await window.TienIchSuaXe.request("/api/services");
      const items = data.services || [];
      catalog.replaceChildren(...items.map(card));
      message.textContent = items.length
        ? ""
        : "Chưa có dịch vụ. Vui lòng quay lại sau.";
      message.hidden = Boolean(items.length);
    } catch (error) {
      message.textContent = error.message;
      retry.hidden = false;
    }
  }
  retry.addEventListener("click", load);
  dialog
    .querySelector("[data-close-dialog]")
    .addEventListener("click", () => dialog.close());
  dialog.addEventListener("click", (event) => {
    if (event.target !== dialog) return;
    const rect = dialog.getBoundingClientRect();
    if (
      event.clientX < rect.left ||
      event.clientX > rect.right ||
      event.clientY < rect.top ||
      event.clientY > rect.bottom
    )
      dialog.close();
  });
  dialog.addEventListener("close", () => trigger?.focus());
  load();
})();
