(() => {
  "use strict";
  const form = document.getElementById("tracking-form");
  if (!form) return;
  const api = window.TienIchSuaXe;
  const result = document.getElementById("tracking-result");
  const message = document.getElementById("tracking-message");
  const empty = document.getElementById("tracking-empty");
  let busy = false;
  const reference = new URLSearchParams(location.search).get("reference");
  if (reference) form.elements.reference.value = reference.slice(0, 40);
  function element(tag, text, className) {
    const node = document.createElement(tag);
    if (text !== undefined) node.textContent = text;
    if (className) node.className = className;
    return node;
  }
  function setBusy(value) {
    busy = value;
    form.querySelectorAll("input, button").forEach((control) => {
      control.disabled = value;
    });
  }
  function showMessage(text, success = false) {
    message.textContent = text;
    message.classList.toggle("success", success);
    message.hidden = !text;
  }
  function render(item, phone) {
    const head = element("div", undefined, "tracking-result-head");
    const heading = element("div");
    heading.append(
      element("span", "MÃ LỊCH HẸN · " + item.reference, "eyebrow"),
      element("h3", "Tiến độ lịch hẹn"),
    );
    head.append(heading, element("span", item.statusLabel, "status-badge"));
    const details = element("div", undefined, "tracking-details");
    [
      ["Mẫu xe", item.vehicleType],
      ["Hãng xe", item.brand],
      ["Biển số", item.licensePlate || "Chưa cập nhật"],
      ["Dịch vụ", item.service],
      ["Thời gian hẹn", api.date(item.date) + " · " + item.time],
      ["Khách hàng", item.customerName],
    ].forEach(([label, value]) => {
      const cell = element("div");
      cell.append(element("small", label), element("strong", value));
      details.append(cell);
    });
    const stages = element("ol", undefined, "tracking-stages");
    (item.stages || []).forEach((stage, index) => {
      const state = ["completed", "current", "upcoming"].includes(stage.state)
        ? stage.state
        : "upcoming";
      const node = element("li", undefined, state);
      node.append(
        element("span", state === "completed" ? "✓" : index + 1),
        element("strong", stage.title),
        element("p", stage.description),
      );
      stages.append(node);
    });
    result.replaceChildren(head, details, stages);
    if (item.processingNote?.trim()) {
      const note = element("section", undefined, "tracking-processing-note");
      note.append(
        element("h4", "Ghi chú từ Cửa Hàng Sửa Xe"),
        element("p", item.processingNote),
      );
      result.append(note);
    }
    if (item.canCancel === true) {
      const cancel = element("button", "Hủy lịch hẹn này", "cancel-booking");
      cancel.type = "button";
      cancel.addEventListener("click", () => {
        if (busy) return;
        cancel.hidden = true;
        const confirmation = element("div", undefined, "cancel-confirmation");
        const yes = element("button", "Xác nhận hủy");
        yes.type = "button";
        const no = element("button", "Giữ lịch hẹn");
        no.type = "button";
        confirmation.append(
          element("p", "Bạn muốn hủy lịch hẹn này?"),
          yes,
          no,
        );
        no.addEventListener("click", () => {
          if (!busy) {
            confirmation.remove();
            cancel.hidden = false;
            cancel.focus();
          }
        });
        yes.addEventListener("click", async () => {
          if (busy) return;
          setBusy(true);
          yes.disabled = true;
          no.disabled = true;
          showMessage("");
          try {
            const updated = await api.request(
              "/api/appointments/" +
                encodeURIComponent(item.reference) +
                "/cancel",
              { method: "POST", body: JSON.stringify({ phone }) },
            );
            render(updated, phone);
            showMessage("Lịch hẹn đã được hủy.", true);
          } catch (error) {
            showMessage(error.message);
            yes.disabled = false;
            no.disabled = false;
          } finally {
            setBusy(false);
          }
        });
        result.append(confirmation);
        yes.focus();
      });
      result.append(cancel);
    }
    result.hidden = false;
    empty.hidden = true;
  }
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (busy || !form.reportValidity()) return;
    const phone = form.elements.phone.value.trim();
    const params = new URLSearchParams({
      reference: form.elements.reference.value.trim(),
      phone,
    });
    setBusy(true);
    showMessage("");
    result.hidden = true;
    empty.hidden = true;
    try {
      render(
        await api.request("/api/repairs/lookup?" + params.toString()),
        phone,
      );
    } catch (error) {
      showMessage(error.message);
      empty.hidden = false;
    } finally {
      setBusy(false);
    }
  });
})();
