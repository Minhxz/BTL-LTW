(() => {
  "use strict";
  const api = window.GiaoTiepMayChu.request,
    content = document.querySelector("#content"),
    dialog = document.querySelector("#editor"),
    form = document.querySelector("#editor-form"),
    body = document.querySelector("#editor-body"),
    save = document.querySelector("#save-editor"),
    error = document.querySelector("#editor-error");
  const role = document.body.dataset.role,
    manager = role === "Manager",
    cashier = role === "Cashier",
    mechanic = role === "Mechanic",
    canReceive = manager || cashier;
  const esc = (x) =>
    String(x ?? "").replace(
      /[&<>"']/g,
      (c) =>
        ({
          "&": "&amp;",
          "<": "&lt;",
          ">": "&gt;",
          '"': "&quot;",
          "'": "&#39;",
        })[c],
    );
  const money = (x) =>
    new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: "VND",
      maximumFractionDigits: 0,
    }).format(Number(x) || 0);
  const date = (x) =>
    x
      ? new Intl.DateTimeFormat("vi-VN", { timeZone: "Asia/Bangkok" }).format(
          new Date(String(x).slice(0, 10) + "T12:00:00+07:00"),
        )
      : "—";
  const today = () =>
    new Intl.DateTimeFormat("sv-SE", { timeZone: "Asia/Bangkok" }).format(
      new Date(),
    );
  let specs = [],
    section = "dashboard",
    page = 1,
    q = "",
    status = "",
    from = "",
    to = "",
    current = [],
    onSave,
    loadingId = 0,
    toastTimer,
    dialogBusy = false;
  const names = {
    dashboard: "Tổng quan cửa hàng",
    bookings: "Lịch hẹn online",
    jobs: "Tiếp nhận & sửa chữa",
    purchases: "Phiếu nhập kho",
    stockHistory: "Lịch sử điều chỉnh",
    invoices: "Hóa đơn thanh toán",
    reports: "Báo cáo doanh thu",
    myAttendance: "Công cá nhân",
    profile: "Tài khoản cá nhân",
  };
  const badge = (s) =>
    `<span class="badge ${["Hoàn thành", "Đã tiếp nhận"].includes(s) ? "green" : ["Chờ sửa", "Chờ xác nhận"].includes(s) ? "amber" : s === "Đang sửa" ? "blue" : s === "Đã hủy" ? "red" : ""}">${esc(s)}</span>`;
  const btn = (text, action, id = "", style = "secondary small") =>
    `<button type="button" class="button ${style}" data-action="${esc(action)}" data-id="${esc(id)}">${esc(text)}</button>`;
  const notice = (text) => {
    const el = document.querySelector("#toast");
    el.textContent = text;
    el.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => (el.hidden = true), 4500);
  };
  const options = async (kind, value = "", optional = true) => {
    const rows = await api("/api/catalog/options/" + kind);
    return (
      (optional ? '<option value="">Chọn…</option>' : "") +
      rows
        .map(
          (x) =>
            `<option value="${esc(x.id)}" ${String(x.id) === String(value) ? "selected" : ""}>${esc(x.label)} [${esc(x.id)}]</option>`,
        )
        .join("")
    );
  };
  const field = (name, label, type = "text", value = "", extra = "") =>
    `<label class="field"><span>${esc(label)}</span><input name="${esc(name)}" type="${type}" value="${esc(value)}" ${extra}/></label>`;
  const selectField = (name, label, html, extra = "required") =>
    `<label class="field"><span>${esc(label)}</span><select name="${name}" ${extra}>${html}</select></label>`;
  function moHopThoai(title, html, handler = null, label = "Lưu thông tin") {
    document.querySelector("#editor-title").textContent = title;
    body.innerHTML = html;
    error.textContent = "";
    onSave = handler;
    save.hidden = !handler;
    save.textContent = label;
    save.disabled = false;
    if (!dialog.open) dialog.showModal();
  }
  function dongHopThoai() {
    if (!dialogBusy) dialog.close();
  }
  document.querySelector("#close-editor").onclick = dongHopThoai;
  document.querySelector("#cancel-editor").onclick = dongHopThoai;
  dialog.addEventListener("cancel", (event) => {
    if (dialogBusy) event.preventDefault();
  });
  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    if (!onSave || dialogBusy || !form.reportValidity()) return;
    error.textContent = "";
    save.disabled = true;
    dialogBusy = true;
    const handler = onSave;
    try {
      const message = await handler(new FormData(form));
      dialogBusy = false;
      dialog.close();
      notice(message || "Đã lưu thông tin.");
      await taiDuLieu();
    } catch (e) {
      error.textContent = e.message;
    } finally {
      save.disabled = false;
      dialogBusy = false;
    }
  });
  document.querySelector("#menu-toggle").onclick = () =>
    document.querySelector("#sidebar").classList.toggle("open");
  document.querySelector("#logout").onclick = async () => {
    try {
      await api("/api/auth/logout", { method: "POST" });
      location.href = "/Account/Login";
    } catch (e) {
      notice(e.message);
    }
  };
  function hienMenu() {
    const groups = [
      [
        "TỔNG QUAN",
        [
          ["dashboard", "◫"],
          ...(canReceive ? [["bookings", "◷"]] : []),
          ...(canReceive || mechanic ? [["jobs", "▱"]] : []),
        ],
      ],
      [
        "KHÁCH HÀNG & DỊCH VỤ",
        ["customers", "vehicles", "services", "serviceParts"].map((x) => [
          x,
          "◇",
        ]),
      ],
      [
        "KHO & NHÀ CUNG CẤP",
        [
          "parts",
          "suppliers",
          ...(manager ? ["purchases", "stockHistory"] : []),
        ].map((x) => [x, "▦"]),
      ],
      [
        "NHÂN SỰ",
        [
          "employees",
          "roles",
          "adjustments",
          "roleAdjustments",
          "shifts",
          "attendance",
          "myAttendance",
        ].map((x) => [x, "♧"]),
      ],
      [
        "TÀI CHÍNH",
        [
          ...(canReceive ? [["invoices", "▤"]] : []),
          ...(manager ? [["reports", "◴"]] : []),
          ["profile", "⚙"],
        ],
      ],
    ];
    document.querySelector("#navigation").innerHTML = groups
      .map(([label, items]) => {
        items = items.filter(
          ([id]) => names[id] || specs.some((x) => x.id === id),
        );
        return items.length
          ? `<div class="nav-label">${label}</div>` +
              items
                .map(
                  ([id, icon]) =>
                    `<a class="nav-item ${id === section ? "active" : ""}" href="#${id}"><i>${icon}</i>${esc(names[id] || specs.find((x) => x.id === id)?.title)}</a>`,
                )
                .join("")
          : "";
      })
      .join("");
  }
  function taoBoLoc({
    dates = false,
    states = false,
    add = false,
    extra = "",
  } = {}) {
    return `<form id="filters" class="toolbar"><input name="q" class="search" placeholder="Tìm mã, tên hoặc thông tin…" aria-label="Tìm kiếm" value="${esc(q)}" maxlength="150"/>${states ? `<select name="status" aria-label="Trạng thái"><option value="">Tất cả trạng thái</option>${["Chờ sửa", "Đang sửa", "Hoàn thành", "Đã hủy"].map((s) => `<option ${s === status ? "selected" : ""}>${s}</option>`).join("")}</select>` : ""}${dates ? `<input name="from" aria-label="Từ ngày" type="date" value="${from}"/><input name="to" aria-label="Đến ngày" type="date" value="${to}"/>` : ""}<button class="button secondary">Lọc</button>${btn("Làm mới", "reset")}${add ? btn("+ Thêm mới", "create", "", "primary") : ""}${extra}</form>`;
  }
  function taoBang(columns, rows, actions) {
    return `<div class="table-wrap"><table><thead><tr>${columns.map((c) => `<th>${esc(c[1])}</th>`).join("")}${actions ? "<th>Thao tác</th>" : ""}</tr></thead><tbody>${rows.length ? rows.map((row, index) => `<tr>${columns.map(([key, label, render]) => `<td>${render ? render(row[key], row) : esc(row[key] ?? "—")}</td>`).join("")}${actions ? `<td><div class="row-actions">${actions(row, index)}</div></td>` : ""}</tr>`).join("") : `<tr><td colspan="${columns.length + (actions ? 1 : 0)}"><div class="empty">Chưa có dữ liệu phù hợp. Thử thay bộ lọc hoặc thêm bản ghi mới.</div></td></tr>`}</tbody></table></div>`;
  }
  function taoPhanTrang(data) {
    return `<div class="pager"><span class="count">${data.total ?? data.items.length} bản ghi</span><button class="button secondary small" data-action="prev" ${page <= 1 ? "disabled" : ""}>← Trước</button><span>Trang ${page} / ${data.totalPages || 1}</span><button class="button secondary small" data-action="next" ${page >= (data.totalPages || 1) ? "disabled" : ""}>Sau →</button></div>`;
  }
  function layBoLoc() {
    return new URLSearchParams({ q, page, pageSize: 12, status, from, to });
  }
  async function taiDuLieu() {
    const turn = ++loadingId;
    document.querySelector("#page-title").textContent =
      names[section] || specs.find((x) => x.id === section)?.title || "Quản lý";
    hienMenu();
    content.innerHTML = '<div class="empty">Đang đọc dữ liệu…</div>';
    try {
      let html = "";
      if (section === "dashboard") html = await hienTongQuan();
      else if (section === "reports") html = await hienBaoCao();
      else if (section === "profile")
        html = `<div class="panel"><h2>Đổi mật khẩu</h2><p class="hint">Mật khẩu được cập nhật cho tài khoản dùng chung của nhóm trên cả website và ứng dụng quản lý.</p>${btn("Đổi mật khẩu", "password", "", "primary")}</div>`;
      else if (section === "myAttendance") {
        const d = await api("/api/workshop/my-attendance?" + layBoLoc());
        html = `<div class="panel"><h2>Tổng giờ thực tế: ${esc(d.totalHours)}</h2>${taoBoLoc({ dates: true })}${taoBang(
          [
            ["date", "Ngày", date],
            ["shift", "Ca"],
            ["start", "Bắt đầu"],
            ["end", "Kết thúc"],
            ["hours", "Giờ thực tế"],
          ],
          d.items,
        )}</div>`;
      } else {
        const spec = specs.find((x) => x.id === section);
        let endpoint = spec
          ? "/api/catalog/" + section
          : {
              jobs: "/api/workshop/jobs",
              bookings: "/api/workshop/bookings",
              purchases: "/api/workshop/stock/purchases",
              stockHistory: "/api/workshop/stock/history",
              invoices: "/api/workshop/invoices",
            }[section];
        if (!endpoint) {
          location.hash = "dashboard";
          return;
        }
        const d = await api(endpoint + "?" + layBoLoc());
        if (turn !== loadingId) return;
        current = d.items;
        if (spec) {
          const cols = spec.fields
            .filter((f) => f.type !== "password")
            .map((f) => [
              f.name,
              f.label,
              (v) =>
                f.type === "bool"
                  ? badge(v ? "Đang hoạt động" : "Ngừng hoạt động")
                  : f.type === "date"
                    ? date(v)
                    : f.type === "money" || f.type === "signedMoney"
                      ? money(v)
                      : esc(v),
            ]);
          html =
            taoBoLoc({
              add: true,
              extra:
                section === "parts"
                  ? btn("Điều chỉnh tồn", "stock-adjust")
                  : "",
            }) +
            taoBang(
              cols,
              d.items,
              (r, i) =>
                `${spec.fields.some((f) => !f.readOnly && !spec.keys.includes(f.name)) ? btn("Sửa", "edit", i) : ""}${section === "services" ? btn("Ảnh & nội dung", "content", r.MaDichVu) : ""}${btn("Xóa", "delete", i)}`,
            );
        }
        if (section === "jobs")
          html =
            taoBoLoc({ dates: true, states: true, add: canReceive }) +
            taoBang(
              [
                ["id", "Mã phiếu"],
                [
                  "customerName",
                  "Khách hàng",
                  (v, r) =>
                    `${esc(v)}<span class="cell-secondary">${esc(r.phone)}</span>`,
                ],
                [
                  "licensePlate",
                  "Xe",
                  (v, r) =>
                    `<b>${esc(v)}</b><span class="cell-secondary">${esc(r.brand)}</span>`,
                ],
                ["dueDate", "Hẹn trả", date],
                ["status", "Tiến độ", badge],
                ["total", "Tổng tiền", money],
                ["paid", "Thu tiền", (v) => badge(v ? "Đã thu" : "Chưa thu")],
              ],
              d.items,
              (r) => btn("Chi tiết", "job", r.id),
            );
        if (section === "bookings")
          html =
            taoBoLoc() +
            taoBang(
              [
                ["id", "Mã lịch"],
                [
                  "customerName",
                  "Khách hàng",
                  (v, r) =>
                    `${esc(v)}<span class="cell-secondary">${esc(r.phone)}</span>`,
                ],
                ["licensePlate", "Biển số"],
                ["service", "Dịch vụ"],
                [
                  "date",
                  "Ngày / giờ hẹn",
                  (v) => `${date(v)} · ${esc(String(v).slice(11, 16))}`,
                ],
                ["note", "Yêu cầu"],
                ["status", "Trạng thái", badge],
              ],
              d.items,
              (r) =>
                r.status === "Chờ xác nhận"
                  ? btn("Tiếp nhận", "accept", r.id) +
                    btn("Hủy", "reject", r.id)
                  : r.jobId
                    ? btn("Xem phiếu", "job", r.jobId)
                    : "",
            );
        if (section === "purchases")
          html =
            taoBoLoc({ add: true }) +
            taoBang(
              [
                ["id", "Mã phiếu"],
                ["date", "Ngày nhập", date],
                ["employee", "Nhân viên"],
                ["note", "Ghi chú"],
                ["amount", "Tổng nhập", money],
              ],
              d.items,
              (r) => btn("Chi tiết", "purchase-detail", r.id),
            );
        if (section === "stockHistory")
          html =
            taoBoLoc() +
            taoBang(
              [
                ["id", "Mã"],
                ["partId", "Phụ tùng"],
                ["kind", "Nội dung"],
                ["date", "Ngày", date],
                ["oldValue", "Trước"],
                ["newValue", "Sau"],
                ["reason", "Lý do"],
                ["employee", "Người thực hiện"],
              ],
              d.items,
            );
        if (section === "invoices")
          html =
            taoBoLoc({ dates: true }) +
            `<p class="hint">Tổng tiền trong kết quả lọc: <b>${money(d.totalAmount)}</b>. Lập hóa đơn từ chi tiết phiếu sửa đã hoàn thành.</p>` +
            taoBang(
              [
                ["id", "Hóa đơn"],
                ["jobId", "Phiếu sửa"],
                ["customerName", "Khách hàng"],
                ["licensePlate", "Biển số"],
                ["paidOn", "Ngày thu", date],
                ["amount", "Thành tiền", money],
              ],
              d.items,
              (r) => btn("Xem / In", "print-invoice", r.jobId),
            );
        html = `<section class="panel">${html}${taoPhanTrang(d)}</section>`;
      }
      if (turn !== loadingId) return;
      content.innerHTML = html;
      document.querySelector("#filters")?.addEventListener("submit", (e) => {
        e.preventDefault();
        const d = new FormData(e.target);
        q = String(d.get("q") || "");
        status = String(d.get("status") || "");
        from = String(d.get("from") || "");
        to = String(d.get("to") || "");
        page = 1;
        taiDuLieu();
      });
    } catch (e) {
      if (turn === loadingId)
        content.innerHTML = `<div class="error-box">${esc(e.message)} ${btn("Thử lại", "reload")}</div>`;
    }
  }
  async function hienTongQuan() {
    const d = await api("/api/workshop/dashboard");
    document.querySelector("#db-status").textContent = "● " + d.database;
    const count = (s) => d.jobs.find((x) => x.status === s)?.count || 0;
    return `<div class="welcome"><div><h2>Mọi hoạt động, trong một nơi.</h2><p class="muted">Theo dõi cửa hàng và tiếp tục công việc hôm nay.</p></div>${canReceive ? btn("+ Tiếp nhận xe", "create-job", "", "primary") : ""}</div><div class="stats"><article class="stat dark"><label>XE ĐANG SỬA</label><strong>${count("Đang sửa")}</strong><small>Đang được thợ xử lý</small></article><article class="stat"><label>CHỜ SỬA</label><strong>${count("Chờ sửa")}</strong><small>Đã tiếp nhận tại cửa hàng</small></article><article class="stat"><label>${canReceive ? "LỊCH CHỜ XÁC NHẬN" : "ĐÃ HOÀN THÀNH"}</label><strong>${canReceive ? d.pendingBookings : count("Hoàn thành")}</strong><small>${canReceive ? "Yêu cầu từ website" : "Xe được giao phụ trách"}</small></article><article class="stat"><label>${canReceive ? "THU HÔM NAY" : "TỔNG PHIẾU"}</label><strong style="font-size:25px">${canReceive ? money(d.todayRevenue) : d.jobs.reduce((s, x) => s + x.count, 0)}</strong><small>${canReceive ? "Từ hóa đơn đã thanh toán" : "Trong phạm vi của bạn"}</small></article></div><div class="grid-two"><section class="panel"><div class="panel-head"><h2>Luồng công việc</h2><a href="/Home/Flow">Xem đầy đủ ↗</a></div><div class="flow-preview">${canReceive ? '<a href="#bookings">01 · Lịch hẹn →</a>' : ""}<a href="#jobs">02 · Tiếp nhận →</a><a href="#jobs">03 · Sửa chữa →</a>${canReceive ? '<a href="#invoices">04 · Thanh toán</a>' : ""}</div><p class="hint">Mọi thay đổi được ghi trực tiếp vào cơ sở dữ liệu của cửa hàng. Số liệu trên màn hình lấy từ các phiếu và hóa đơn thực tế.</p></section><section class="panel"><div class="panel-head"><h2>Trạng thái phiếu sửa</h2></div>${taoBang(
      [
        ["status", "Tiến độ", badge],
        ["count", "Số phiếu"],
      ],
      d.jobs,
    )}</section></div>${
      d.stockAlerts
        ? `<section class="panel"><div class="panel-head"><h2>Phụ tùng cần chú ý</h2><a href="#parts">Đến kho →</a></div>${taoBang(
            [
              ["id", "Mã"],
              ["name", "Phụ tùng"],
              ["stock", "Tồn hiện tại"],
              ["expiry", "Hạn sử dụng", date],
            ],
            d.stockAlerts,
          )}<p class="hint">Tồn không quá 5 hoặc hết hạn trong 30 ngày tới.</p></section>`
        : ""
    }`;
  }
  async function hienBaoCao() {
    if (!from && !to) {
      from = today().slice(0, 8) + "01";
      to = today();
    }
    const d = await api("/api/workshop/reports?" + layBoLoc());
    const max = Math.max(1, ...d.daily.map((x) => x.amount));
    return (
      taoBoLoc({
        dates: true,
        extra: `<a class="button primary" href="/api/workshop/reports/export?${new URLSearchParams({ from, to })}">Tải CSV</a>`,
      }) +
      `<div class="stats"><article class="stat dark"><label>DOANH THU ĐÃ THU</label><strong style="font-size:24px">${money(d.paid)}</strong><small>Theo ngày thanh toán</small></article><article class="stat"><label>GIÁ TRỊ NHẬP KHO</label><strong style="font-size:24px">${money(d.purchases)}</strong><small>Theo ngày nhập</small></article><article class="stat"><label>THU TRỪ NHẬP KHO</label><strong style="font-size:24px">${money(d.cashDifference)}</strong><small>Không phải lợi nhuận kế toán</small></article><article class="stat"><label>HOÀN THÀNH CHƯA THU</label><strong style="font-size:24px">${money(d.unpaid)}</strong><small>Tất cả thời gian</small></article></div><div class="grid-two"><section class="panel"><h2>Doanh thu theo ngày</h2><div class="chart-bars">${d.daily.length ? d.daily.map((x) => `<div class="chart-row"><span>${date(x.date)}</span><div class="chart-track"><span style="width:${Math.max(1, (x.amount / max) * 100)}%"></span></div><strong>${money(x.amount)}</strong></div>`).join("") : '<div class="empty">Chưa có khoản thu trong kỳ.</div>'}</div></section><section class="panel"><h2>Dịch vụ được sử dụng</h2>${taoBang(
        [
          ["name", "Dịch vụ"],
          ["quantity", "Lượt"],
          ["amount", "Doanh thu", money],
        ],
        d.services,
      )}</section></div>`
    );
  }
  function layKhoa(spec, row) {
    return spec.keys
      .map((k) => {
        const f = spec.fields.find((x) => x.name === k);
        return f.type === "date" ? String(row[k]).slice(0, 10) : row[k];
      })
      .join("~");
  }
  async function suaDanhMuc(index) {
    const spec = specs.find((x) => x.id === section),
      row = index == null ? null : current[index];
    const fields = await Promise.all(
      spec.fields
        .filter((f) => !f.readOnly)
        .map(async (f) => {
          const fixed = row && spec.keys.includes(f.name),
            v = row?.[f.name] ?? (f.type === "bool" ? true : "");
          if (f.type === "select" && !fixed)
            return selectField(f.name, f.label, await options(f.source, v));
          if (f.type === "textarea")
            return `<label class="field full"><span>${esc(f.label)}</span><textarea name="${f.name}" rows="3" maxlength="${f.max}" required>${esc(v)}</textarea></label>`;
          if (f.type === "bool")
            return `<label class="field"><span>${esc(f.label)}</span><input name="${f.name}" type="checkbox" ${v ? "checked" : ""}/></label>`;
          const type =
            f.type === "date"
              ? "date"
              : f.type === "time"
                ? "time"
                : f.type === "password"
                  ? "password"
                  : [
                        "money",
                        "signedMoney",
                        "positiveInt",
                        "int",
                        "factor",
                        "hours",
                      ].includes(f.type)
                    ? "number"
                    : "text";
          const value =
            f.type === "date"
              ? String(v).slice(0, 10)
              : f.type === "time"
                ? String(v).slice(0, 5)
                : f.type === "password"
                  ? ""
                  : v;
          const bounds =
            f.type === "money" || f.type === "positiveInt"
              ? 'min="1" step="1"'
              : f.type === "factor"
                ? 'min="0.01" max="99.99" step="0.01"'
                : f.type === "hours"
                  ? 'min="0" max="24" step="0.01"'
                  : type === "number"
                    ? 'step="1"'
                    : "";
          return field(
            f.name,
            f.label,
            type,
            value,
            `${fixed ? "readonly" : ""} ${!f.optional ? "required" : ""} maxlength="${f.max}" ${bounds}`,
          );
        }),
    );
    moHopThoai(
      (row ? "Cập nhật · " : "Thêm · ") + spec.title,
      `<div class="form-grid">${fields.join("")}</div>${section === "parts" ? '<p class="hint">Phụ tùng mới có tồn 0. Dùng Phiếu nhập để tăng tồn hoặc Điều chỉnh tồn khi kiểm kê.</p>' : ""}`,
      async (d) => {
        const payload = Object.fromEntries(d);
        for (const f of spec.fields)
          if (f.type === "bool")
            payload[f.name] = form.elements[f.name].checked;
        const r = await api(
          "/api/catalog/" +
            section +
            (row ? "/" + encodeURIComponent(layKhoa(spec, row)) : ""),
          { method: row ? "PUT" : "POST", body: JSON.stringify(payload) },
        );
        return r.message;
      },
    );
  }
  async function tiepNhanXe() {
    const [serviceOptions, mechanics] = await Promise.all([
      options("services"),
      options("mechanics"),
    ]);
    moHopThoai(
      "Tiếp nhận xe",
      `<div class="form-grid">${field("customerName", "Họ tên khách hàng", "text", "", 'required maxlength="100"')}${field("phone", "Số điện thoại", "tel", "", 'required pattern="0[0-9]{8,10}" maxlength="11"')}${field("licensePlate", "Biển số xe", "text", "", 'required maxlength="20"')}${field("vehicleType", "Loại xe", "text", "Xe máy", 'required maxlength="50"')}${field("brand", "Hãng xe", "text", "", 'required maxlength="50"')}${field("color", "Màu sắc", "text", "", 'required maxlength="30"')}${field("dueDate", "Ngày hẹn trả", "date", today(), `required min="${today()}"`)}${selectField("mechanicId", "Phân công thợ (có thể bổ sung sau)", mechanics, "")}</div><h3 style="margin-top:24px">Dịch vụ thực hiện</h3><div class="line-items" id="lines"></div>${btn("+ Thêm dịch vụ", "line-add")}<p class="hint">Giá trọn gói được chốt từ danh mục. Hệ thống kiểm tra định mức, hạn dùng và tồn kho trước khi tiếp nhận.</p>`,
      async (d) => {
        const input = Object.fromEntries(d);
        input.services = [...body.querySelectorAll(".line-row")].map((r) => ({
          serviceId: r.querySelector("select").value,
          quantity: Number(r.querySelector("input").value),
        }));
        delete input.line;
        const r = await api("/api/workshop/jobs", {
          method: "POST",
          body: JSON.stringify(input),
        });
        return r.message + " Mã " + r.id;
      },
      "Tiếp nhận và trừ kho",
    );
    body.dataset.lineOptions = serviceOptions;
    themDong();
  }
  function themDong() {
    const lines = document.querySelector("#lines");
    // Mỗi form tự khai báo số lượng tối đa (phiếu nhập cho phép tới 100.000 như API).
    const max = Number(lines.dataset.max) || 1000;
    lines.insertAdjacentHTML(
      "beforeend",
      `<div class="line-row"><select required aria-label="Dịch vụ hoặc phụ tùng">${body.dataset.lineOptions}</select><input aria-label="Số lượng" type="number" value="1" min="1" max="${max}" step="1" required/><button type="button" data-action="line-remove" aria-label="Xóa dòng">×</button></div>`,
    );
  }
  async function nhapKho() {
    const parts = await options("parts");
    moHopThoai(
      "Lập phiếu nhập kho",
      `<div class="form-grid">${field("date", "Ngày nhập", "date", today(), `required max="${today()}"`)}${field("note", "Ghi chú", "text", "", 'maxlength="255"')}</div><h3 style="margin-top:24px">Phụ tùng nhập</h3><div class="line-items" id="lines" data-max="100000"></div>${btn("+ Thêm phụ tùng", "line-add")}<p class="hint">Đơn giá lấy từ lô phụ tùng. Tạo mã lô mới khi giá nhập hoặc hạn sử dụng thay đổi. Phiếu đã ghi sổ giữ nguyên; sai lệch được xử lý bằng điều chỉnh tồn có lý do.</p>`,
      async (d) => {
        const input = Object.fromEntries(d);
        input.lines = [...body.querySelectorAll(".line-row")].map((r) => ({
          partId: r.querySelector("select").value,
          quantity: Number(r.querySelector("input").value),
        }));
        return (
          await api("/api/workshop/stock/purchases", {
            method: "POST",
            body: JSON.stringify(input),
          })
        ).message;
      },
      "Lưu phiếu và cộng kho",
    );
    body.dataset.lineOptions = parts;
    themDong();
  }
  async function xemPhieu(id, invoiceOnly = false) {
    const d = await api("/api/workshop/jobs/" + encodeURIComponent(id)),
      j = d.job;
    let html = `${invoiceOnly ? `<div class="invoice-heading"><span class="eyebrow">CỬA HÀNG SỬA XE · DỊCH VỤ SỬA XE</span><h2>HÓA ĐƠN THANH TOÁN</h2><p>${esc(d.invoice?.id || "")}</p></div>` : ""}<div class="detail-grid">${[
      ["Mã phiếu", j.id],
      ["Khách hàng", j.customerName],
      ["Số điện thoại", j.phone],
      ["Biển số", j.licensePlate],
      ["Ngày nhận", date(j.createdOn)],
      ["Hẹn trả", date(j.dueDate)],
    ]
      .map(([k, v]) => `<div><label>${k}</label><b>${esc(v)}</b></div>`)
      .join("")}</div>${taoBang(
      [
        ["serviceId", "Mã dịch vụ"],
        ["name", "Tên dịch vụ"],
        ["quantity", "Số lượng"],
        ["amount", "Thành tiền", money],
      ],
      d.lines,
    )}<div class="invoice-total">Tổng cộng: <b>${money(j.total)}</b></div>`;
    if (invoiceOnly) {
      html += `<p>Ngày thanh toán: <b>${date(d.invoice?.paidOn)}</b></p><p class="hint">Hóa đơn nội bộ phục vụ bài tập, không phải hóa đơn điện tử thuế.</p><div class="detail-actions no-print">${btn("In hóa đơn", "print", "", "primary")}</div>`;
      moHopThoai("Hóa đơn · " + (d.invoice?.id || j.id), html);
      return;
    }
    html += `<h3>Phân công thợ</h3>${taoBang(
      [
        ["name", "Nhân viên"],
        [
          "start",
          "Bắt đầu",
          (v) => `${date(v)} ${esc(String(v).slice(11, 16))}`,
        ],
        [
          "end",
          "Dự kiến xong",
          (v) => `${date(v)} ${esc(String(v).slice(11, 16))}`,
        ],
      ],
      d.assignments,
      canReceive && j.status === "Chờ sửa"
        ? (r) => btn("Bỏ phân công", "unassign", j.id + "~" + r.employeeId)
        : null,
    )}<div class="detail-actions">${canReceive && ["Chờ sửa", "Đang sửa"].includes(j.status) ? btn("Phân công / đổi giờ", "assign", id) + btn("Sửa ngày hẹn", "due-date", id) : ""}${j.status === "Chờ sửa" ? btn("Bắt đầu sửa", "status", id + "~Đang sửa", "primary") : ""}${j.status === "Đang sửa" ? btn("Hoàn thành", "status", id + "~Hoàn thành", "primary") : ""}${canReceive && j.status === "Chờ sửa" ? btn("Hủy và hoàn kho", "status", id + "~Đã hủy", "danger") : ""}${canReceive && j.status === "Hoàn thành" && !j.paid ? btn("Lập hóa đơn / Thu tiền", "payment", id, "primary") : ""}${canReceive && j.paid ? btn("Xem / In hóa đơn", "print-invoice", id) : ""}</div>`;
    moHopThoai("Phiếu " + id + " · " + j.status, html);
  }
  async function nhanLich(id) {
    const mechanics = await options("mechanics");
    moHopThoai(
      "Tiếp nhận lịch " + id,
      `<p class="hint">Kiểm tra thông tin khách trước khi lập phiếu và trừ kho. Thông tin khách và dịch vụ lấy từ lịch hẹn.</p><div class="form-grid">${field("dueDate", "Ngày hẹn trả", "date", today(), `required min="${today()}"`)}${field("color", "Màu xe", "text", "", 'required maxlength="30"')}${selectField("mechanicId", "Thợ phụ trách", mechanics, "")}</div>`,
      async (d) =>
        (
          await api(
            "/api/workshop/bookings/" + encodeURIComponent(id) + "/accept",
            { method: "POST", body: JSON.stringify(Object.fromEntries(d)) },
          )
        ).message,
      "Tiếp nhận và trừ kho",
    );
  }
  function xacNhan(title, text, callback) {
    moHopThoai(
      title,
      `<p>${esc(text)}</p>`,
      async () => {
        const r = await callback();
        return r?.message;
      },
      "Xác nhận",
    );
  }
  function locNoiDung(value) {
    value = String(value || "")
      .replace(/<(\/?)b\s*>/gi, "<$1strong>")
      .replace(/<(\/?)i\s*>/gi, "<$1em>");
    const doc = new DOMParser().parseFromString(
      String(value || ""),
      "text/html",
    );
    const allowed = new Set(["P", "BR", "STRONG", "EM", "UL", "OL", "LI"]);
    function copy(n) {
      if (n.nodeType === 3) return document.createTextNode(n.textContent);
      const el = allowed.has(n.nodeName)
        ? document.createElement(n.nodeName.toLowerCase())
        : document.createDocumentFragment();
      if (["SCRIPT", "STYLE", "IFRAME", "SVG", "OBJECT"].includes(n.nodeName))
        return el;
      for (const child of n.childNodes) el.append(copy(child));
      return el;
    }
    const wrap = document.createElement("div");
    for (const n of doc.body.childNodes) wrap.append(copy(n));
    return wrap.innerHTML;
  }
  async function suaNoiDungDichVu(id) {
    const d = await api(
      "/api/workshop/services/" + encodeURIComponent(id) + "/content",
    );
    moHopThoai(
      "Ảnh và nội dung dịch vụ",
      `<label class="field"><span>Ảnh JPG/PNG/WebP, tối đa 2 MB</span><input type="file" id="service-image" accept="image/jpeg,image/png,image/webp"/></label><input type="hidden" name="imageUrl" value="${esc(d.imageUrl)}"/><div id="image-note" class="hint">${d.imageUrl ? "Đang dùng ảnh đã lưu." : "Chưa có ảnh riêng."}</div><div class="editor-tools">${btn("B · Đậm", "bold")}${btn("I · Nghiêng", "italic")}${btn("• Danh sách", "list")}</div><textarea id="mo-ta-html" name="description" hidden></textarea><div contenteditable="true" role="textbox" aria-label="Nội dung dịch vụ" class="content-editor" id="rich-editor">${locNoiDung(d.description)}</div><p class="hint">Nội dung hiển thị công khai ở trang dịch vụ. Chỉ giữ các định dạng văn bản an toàn.</p>`,
      async (f) =>
        (
          await api(
            "/api/workshop/services/" + encodeURIComponent(id) + "/content",
            {
              method: "PUT",
              body: JSON.stringify({
                description: (document.querySelector("#mo-ta-html").value =
                  locNoiDung(document.querySelector("#rich-editor").innerHTML)),
                imageUrl: f.get("imageUrl"),
              }),
            },
          )
        ).message,
    );
    document.querySelector("#service-image").onchange = async (e) => {
      if (!e.target.files[0]) return;
      const f = new FormData();
      f.append("image", e.target.files[0]);
      save.disabled = true;
      try {
        const result = await api("/api/services/image", {
          method: "POST",
          body: f,
        });
        form.elements.imageUrl.value = result.imageUrl;
        document.querySelector("#image-note").textContent =
          "Ảnh đã tải lên. Bấm Lưu thông tin để áp dụng.";
      } catch (e) {
        error.textContent = e.message;
      } finally {
        save.disabled = false;
      }
    };
  }
  async function xuLyThaoTac(action, id, target) {
    if (action === "reload") return taiDuLieu();
    if (action === "reset") {
      q = "";
      from = "";
      to = "";
      status = "";
      page = 1;
      return taiDuLieu();
    }
    if (action === "prev") {
      page--;
      return taiDuLieu();
    }
    if (action === "next") {
      page++;
      return taiDuLieu();
    }
    if (action === "create")
      return section === "jobs"
        ? tiepNhanXe()
        : section === "purchases"
          ? nhapKho()
          : suaDanhMuc(null);
    if (action === "create-job") return tiepNhanXe();
    if (action === "edit") return suaDanhMuc(Number(id));
    if (action === "delete") {
      const spec = specs.find((s) => s.id === section),
        row = current[Number(id)],
        code = layKhoa(spec, row);
      return xacNhan(
        "Xóa bản ghi",
        "Xóa " +
          code +
          "? Bản ghi đã được dùng trong nghiệp vụ sẽ được bảo vệ bởi liên kết dữ liệu.",
        () =>
          api("/api/catalog/" + section + "/" + encodeURIComponent(code), {
            method: "DELETE",
          }),
      );
    }
    if (action === "job") return xemPhieu(id);
    if (action === "accept") return nhanLich(id);
    if (action === "reject")
      return xacNhan("Hủy lịch hẹn", "Hủy yêu cầu chưa tiếp nhận này?", () =>
        api("/api/workshop/bookings/" + encodeURIComponent(id) + "/reject", {
          method: "POST",
        }),
      );
    if (action === "line-add") return themDong();
    if (action === "line-remove") {
      target.closest(".line-row").remove();
      return;
    }
    if (action === "status") {
      const [job, status] = id.split("~");
      return xacNhan(
        "Cập nhật tiến độ",
        status === "Đã hủy"
          ? "Hủy phiếu và hoàn lại phụ tùng đã xuất?"
          : "Chuyển phiếu sang “" + status + "”?",
        () =>
          api("/api/workshop/jobs/" + job + "/status", {
            method: "PUT",
            body: JSON.stringify({ status }),
          }),
      );
    }
    if (action === "assign") {
      const choices = await options("mechanics");
      return moHopThoai(
        "Phân công thợ",
        `<div class="form-grid">${selectField("employeeId", "Thợ phụ trách", choices)}${field("start", "Bắt đầu", "datetime-local", today() + "T08:00", "required")}${field("end", "Kết thúc dự kiến", "datetime-local", today() + "T17:00", "required")}</div><p class="hint">Thợ chính tối đa 3 xe đang xử lý, thợ phụ tối đa 2 xe. Chọn lại cùng thợ để cập nhật thời gian.</p>`,
        async (d) =>
          (
            await api("/api/workshop/jobs/" + id + "/assign", {
              method: "POST",
              body: JSON.stringify(Object.fromEntries(d)),
            })
          ).message,
      );
    }
    if (action === "unassign") {
      const [job, employee] = id.split("~");
      return xacNhan(
        "Bỏ phân công",
        "Bỏ thợ này khỏi phiếu đang chờ sửa?",
        () =>
          api("/api/workshop/jobs/" + job + "/assign/" + employee, {
            method: "DELETE",
          }),
      );
    }
    if (action === "due-date")
      return moHopThoai(
        "Cập nhật ngày hẹn",
        field(
          "dueDate",
          "Ngày hẹn trả",
          "date",
          today(),
          `required min="${today()}"`,
        ),
        async (d) =>
          (
            await api("/api/workshop/jobs/" + id + "/due-date", {
              method: "PUT",
              body: JSON.stringify(Object.fromEntries(d)),
            })
          ).message,
      );
    if (action === "payment") {
      const d = await api("/api/workshop/jobs/" + id);
      return moHopThoai(
        "Thu tiền phiếu " + id,
        `<p>Khách hàng: <b>${esc(d.job.customerName)}</b></p><p>Tổng tiền cần thu: <b>${money(d.job.total)}</b></p>${field("paidOn", "Ngày thanh toán", "date", today(), `required max="${today()}" min="${String(d.job.createdOn).slice(0, 10)}"`)}<p class="hint">Xác nhận khi đã nhận đủ tiền. Tổng tiền lấy từ chi tiết phiếu, không nhập lại thủ công.</p>`,
        async (f) =>
          (
            await api("/api/workshop/jobs/" + id + "/invoice", {
              method: "POST",
              body: JSON.stringify(Object.fromEntries(f)),
            })
          ).message,
        "Xác nhận đã thu tiền",
      );
    }
    if (action === "print-invoice") return xemPhieu(id, true);
    if (action === "print") return window.print();
    if (action === "purchase-detail") {
      const d = await api(
        "/api/workshop/stock/purchases/" + encodeURIComponent(id),
      );
      const total = d.lines.reduce((sum, l) => sum + Number(l.amount), 0);
      return moHopThoai(
        "Chi tiết phiếu nhập " + id,
        taoBang(
          [
            ["partId", "Mã phụ tùng"],
            ["name", "Tên"],
            ["quantity", "Số lượng"],
            ["price", "Đơn giá nhập", money],
            ["amount", "Thành tiền", money],
          ],
          d.lines,
        ) + `<p class="hint"><strong>Tổng nhập: ${money(total)}</strong></p>`,
      );
    }
    if (action === "stock-adjust") {
      const choices = await options("parts");
      return moHopThoai(
        "Điều chỉnh tồn sau kiểm kê",
        `<div class="form-grid">${selectField("partId", "Phụ tùng", choices)}${field("stock", "Số tồn thực tế", "number", "", 'required min="0" max="1000000" step="1"')}${field("reason", "Lý do điều chỉnh", "text", "", 'required minlength="5" maxlength="255"')}</div>`,
        async (d) => {
          const p = Object.fromEntries(d);
          p.stock = Number(p.stock);
          return (
            await api("/api/workshop/stock/adjust", {
              method: "POST",
              body: JSON.stringify(p),
            })
          ).message;
        },
      );
    }
    if (action === "password")
      return moHopThoai(
        "Đổi mật khẩu",
        `<div class="form-grid">${field("oldPassword", "Mật khẩu hiện tại", "password", "", 'required autocomplete="current-password"')}${field("newPassword", "Mật khẩu mới", "password", "", 'required minlength="6" maxlength="100" autocomplete="new-password"')}</div>`,
        async (d) =>
          (
            await api("/api/auth/password", {
              method: "PUT",
              body: JSON.stringify(Object.fromEntries(d)),
            })
          ).message,
      );
    if (action === "content") return suaNoiDungDichVu(id);
    if (["bold", "italic", "list"].includes(action)) {
      document.querySelector("#rich-editor").focus();
      document.execCommand(action === "list" ? "insertUnorderedList" : action);
    }
  }
  document.addEventListener("click", async (e) => {
    const target = e.target.closest("[data-action]");
    if (!target || target.disabled) return;
    try {
      await xuLyThaoTac(target.dataset.action, target.dataset.id, target);
    } catch (ex) {
      if (dialog.open) error.textContent = ex.message;
      else notice(ex.message);
    }
  });
  function chuyenTrang() {
    const next = location.hash.slice(1) || "dashboard";
    if (next !== section) {
      page = 1;
      q = "";
      from = "";
      to = "";
      status = "";
    }
    section = next;
    document.querySelector("#sidebar").classList.remove("open");
    taiDuLieu();
  }
  window.addEventListener("hashchange", chuyenTrang);
  document.querySelector("#today").textContent = new Intl.DateTimeFormat(
    "vi-VN",
    { dateStyle: "full" },
  ).format(new Date());
  api("/api/catalog/schema")
    .then((data) => {
      specs = data;
      chuyenTrang();
    })
    .catch((e) => {
      content.innerHTML = `<div class="error-box">${esc(e.message)} <a href="/Account/Login">Đăng nhập lại</a></div>`;
    });
})();
