(() => {
  "use strict";
  const form = document.getElementById("booking-form");
  if (!form) return;
  const api = window.TienIchSuaXe;
  const date = form.elements.date;
  const time = form.elements.time;
  const service = form.elements.service;
  const message = document.getElementById("booking-message");
  const submit = document.getElementById("booking-submit");
  const success = document.getElementById("booking-success");
  const saveDraft = document.getElementById("booking-save-draft");
  const restoreDraft = document.getElementById("booking-restore-draft");
  const clearDraft = document.getElementById("booking-clear-draft");
  const draftMessage = document.getElementById("booking-draft-message");
  const retryServices = document.getElementById("booking-services-retry");
  const fields = [
    "customerName",
    "phone",
    "vehicleType",
    "brand",
    "licensePlate",
    "service",
    "date",
    "time",
    "note",
  ];
  const query = new URLSearchParams(location.search);
  let servicesReady = false,
    busy = false,
    hasDraft = false,
    draftVersion = 0;
  const today = api.today();
  date.min = today;
  const max = new Date(today + "T12:00:00+07:00");
  max.setDate(max.getDate() + 90);
  date.max = new Intl.DateTimeFormat("sv-SE", {
    timeZone: "Asia/Bangkok",
  }).format(max);
  function updateTimes() {
    const now = new Intl.DateTimeFormat("en-GB", {
      timeZone: "Asia/Bangkok",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
    }).format(new Date());
    [...time.options].forEach((option) => {
      option.disabled = Boolean(
        option.value && date.value === api.today() && option.value <= now,
      );
    });
    if (time.selectedOptions[0]?.disabled) time.value = "";
  }
  date.addEventListener("change", updateTimes);
  function updateActions() {
    saveDraft.disabled = busy;
    restoreDraft.disabled = busy || !hasDraft || !servicesReady;
    clearDraft.hidden = !hasDraft;
    clearDraft.disabled = busy;
    submit.disabled = busy || !servicesReady;
    service.disabled = busy || !servicesReady;
  }
  function setBusy(value) {
    busy = value;
    form.querySelectorAll("input, select, textarea").forEach((input) => {
      input.disabled = value;
    });
    retryServices.disabled = value;
    updateActions();
  }
  function draftStatus(text, error = false) {
    draftMessage.textContent = text;
    draftMessage.classList.toggle("draft-error", error);
  }
  function values() {
    return Object.fromEntries(
      fields.map((name) => [name, form.elements[name].value.trim()]),
    );
  }
  async function loadServices() {
    retryServices.hidden = true;
    service.disabled = true;
    try {
      const data = await api.request("/api/services");
      const items = data.services || [];
      service.replaceChildren(
        new Option(items.length ? "Chọn dịch vụ" : "Chưa có dịch vụ", ""),
        ...items.map((item) => new Option(item.name, item.id)),
      );
      servicesReady = Boolean(items.length);
      const selected = query.get("service");
      if ([...service.options].some((option) => option.value === selected))
        service.value = selected;
      if (!items.length) {
        message.textContent =
          "Hiện chưa có dịch vụ để đặt lịch. Vui lòng quay lại sau.";
        message.hidden = false;
      } else {
        message.hidden = true;
      }
    } catch (error) {
      service.replaceChildren(new Option("Không tải được dịch vụ", ""));
      message.textContent = error.message;
      message.hidden = false;
      retryServices.hidden = false;
    } finally {
      updateActions();
    }
  }
  retryServices.addEventListener("click", loadServices);
  saveDraft.addEventListener("click", async () => {
    if (busy) return;
    const draft = values();
    draftVersion++;
    setBusy(true);
    try {
      await api.request("/api/appointments/draft", {
        method: "PUT",
        body: JSON.stringify(draft),
      });
      hasDraft = true;
      draftStatus(
        "Đã lưu bản nháp trong phiên này. Bạn có thể khôi phục khi quay lại.",
      );
    } catch (error) {
      draftStatus(error.message, true);
    } finally {
      setBusy(false);
    }
  });
  restoreDraft.addEventListener("click", async () => {
    if (busy || !servicesReady) return;
    draftVersion++;
    setBusy(true);
    try {
      const data = await api.request("/api/appointments/draft");
      hasDraft = Boolean(data.draft);
      if (!hasDraft) {
        draftStatus("Không còn bản nháp trong phiên này.");
        return;
      }
      fields.forEach((name) => {
        form.elements[name].value =
          typeof data.draft[name] === "string" ? data.draft[name] : "";
      });
      // Bản nháp từ form cũ lưu nhóm phương tiện thay cho mẫu xe.
      const legacyType = ["Ô tô", "Xe máy", "Xe điện & hybrid", "Xe bán tải / xe tải nhẹ"]
        .includes(data.draft.vehicleType);
      if (legacyType) {
        form.elements.vehicleType.value = "";
        form.elements.brand.value = "";
      }
      updateTimes();
      draftStatus(
        legacyType
          ? "Đã khôi phục bản nháp cũ. Vui lòng nhập lại mẫu xe và hãng xe."
          : "Đã khôi phục bản nháp. Kiểm tra lại dịch vụ, ngày và giờ trước khi gửi.",
      );
    } catch (error) {
      draftStatus(error.message, true);
    } finally {
      setBusy(false);
    }
  });
  clearDraft.addEventListener("click", async () => {
    if (busy) return;
    draftVersion++;
    setBusy(true);
    try {
      await api.request("/api/appointments/draft", { method: "DELETE" });
      hasDraft = false;
      draftStatus(
        "Đã xóa bản nháp. Thông tin đang nhập vẫn được giữ trên form.",
      );
    } catch (error) {
      draftStatus(error.message, true);
    } finally {
      setBusy(false);
    }
  });
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (busy || !servicesReady) return;
    updateTimes();
    if (!form.reportValidity()) return;
    const appointment = values();
    draftVersion++;
    setBusy(true);
    message.hidden = true;
    submit.textContent = "ĐANG GỬI YÊU CẦU…";
    try {
      const result = await api.request("/api/appointments", {
        method: "POST",
        body: JSON.stringify(appointment),
      });
      document.getElementById("success-reference").textContent =
        result.reference;
      document.getElementById("success-summary").textContent =
        api.date(result.date) + " · " + result.time + " · " + result.service;
      document.getElementById("success-tracking").href =
        "/Home/Tracking?reference=" + encodeURIComponent(result.reference);
      try {
        await api.request("/api/appointments/draft", { method: "DELETE" });
        hasDraft = false;
        draftStatus("Bản nháp được lưu tạm trong phiên trình duyệt này.");
      } catch {
        draftStatus(
          "Lịch hẹn đã gửi thành công. Bản nháp cũ chưa được xóa; bạn có thể xóa khi đặt lịch khác.",
          true,
        );
      }
      form.hidden = true;
      success.hidden = false;
      success.scrollIntoView({ behavior: "smooth", block: "center" });
    } catch (error) {
      message.classList.remove("success");
      message.textContent = error.message;
      message.hidden = false;
      const field = Object.keys(error.fields || {})[0];
      const input = [...form.elements].find(
        (item) => item.name?.toLowerCase() === field?.toLowerCase(),
      );
      setBusy(false);
      input?.focus();
    } finally {
      setBusy(false);
      submit.textContent = "GỬI YÊU CẦU ĐẶT LỊCH ↗";
    }
  });
  document
    .getElementById("copy-reference")
    .addEventListener("click", async (event) => {
      try {
        await navigator.clipboard.writeText(
          document.getElementById("success-reference").textContent,
        );
        event.target.textContent = "Đã sao chép";
      } catch {
        event.target.textContent = "Chọn mã để sao chép";
        const selection = window.getSelection();
        const range = document.createRange();
        range.selectNodeContents(document.getElementById("success-reference"));
        selection.removeAllRanges();
        selection.addRange(range);
      }
    });
  document.getElementById("new-booking").addEventListener("click", () => {
    success.hidden = true;
    form.hidden = false;
    form.reset();
    message.hidden = true;
    updateTimes();
    updateActions();
    document.getElementById("copy-reference").textContent = "Sao chép mã";
    form.elements.customerName.focus();
  });
  async function checkDraft() {
    const version = draftVersion;
    try {
      const data = await api.request("/api/appointments/draft");
      if (version !== draftVersion) return;
      hasDraft = Boolean(data.draft);
      if (hasDraft)
        draftStatus(
          "Có bản nháp trong phiên này. Chọn “Khôi phục bản nháp” để tiếp tục.",
        );
    } catch {
      if (version === draftVersion)
        draftStatus(
          "Chưa tải được bản nháp. Bạn vẫn có thể điền và gửi lịch hẹn.",
          true,
        );
    } finally {
      updateActions();
    }
  }
  loadServices();
  checkDraft();
})();
