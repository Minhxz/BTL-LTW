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
  // Màu nhãn trạng thái: xanh lá = xong, vàng = đang chờ, xanh dương = đang sửa, đỏ = đã hủy.
  function mauTrangThai(trangThai) {
    if (["Hoàn thành", "Đã tiếp nhận"].includes(trangThai)) return "green";
    if (["Chờ sửa", "Chờ xác nhận"].includes(trangThai)) return "amber";
    if (trangThai === "Đang sửa") return "blue";
    if (trangThai === "Đã hủy") return "red";
    return "";
  }
  const badge = (s) =>
    `<span class="badge ${mauTrangThai(s)}">${esc(s)}</span>`;
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
        if (!items.length) return "";

        const cacMuc = items.map(([id, icon]) => {
          const tenMuc = names[id] || specs.find((x) => x.id === id)?.title;
          const dangChon = id === section ? "active" : "";
          return `<a class="nav-item ${dangChon}" href="#${id}"><i>${icon}</i>${esc(tenMuc)}</a>`;
        });

        return `<div class="nav-label">${label}</div>` + cacMuc.join("");
      })
      .join("");
  }
  function taoBoLoc({
    dates = false,
    states = false,
    add = false,
    extra = "",
  } = {}) {
    const oTimKiem = `<input name="q" class="search" placeholder="Tìm mã, tên hoặc thông tin…" aria-label="Tìm kiếm" value="${esc(q)}" maxlength="150"/>`;

    let oTrangThai = "";
    if (states) {
      const cacTrangThai = ["Chờ sửa", "Đang sửa", "Hoàn thành", "Đã hủy"]
        .map((s) => `<option ${s === status ? "selected" : ""}>${s}</option>`)
        .join("");
      oTrangThai = `<select name="status" aria-label="Trạng thái"><option value="">Tất cả trạng thái</option>${cacTrangThai}</select>`;
    }

    let oNgay = "";
    if (dates) {
      oNgay =
        `<input name="from" aria-label="Từ ngày" type="date" value="${from}"/>` +
        `<input name="to" aria-label="Đến ngày" type="date" value="${to}"/>`;
    }

    return [
      `<form id="filters" class="toolbar">`,
      oTimKiem,
      oTrangThai,
      oNgay,
      `<button class="button secondary">Lọc</button>`,
      btn("Làm mới", "reset"),
      add ? btn("+ Thêm mới", "create", "", "primary") : "",
      extra,
      `</form>`,
    ].join("");
  }

  // columns: [[tên cột dữ liệu, tiêu đề, hàm hiển thị (không bắt buộc)], ...]
  // actions: hàm (dòng, vị trí) → các nút thao tác của dòng đó (không bắt buộc).
  function taoBang(columns, rows, actions) {
    const tieuDe =
      columns.map((c) => `<th>${esc(c[1])}</th>`).join("") +
      (actions ? "<th>Thao tác</th>" : "");

    const taoDong = (row, index) => {
      const cacO = columns
        .map(([key, label, render]) => `<td>${render ? render(row[key], row) : esc(row[key] ?? "—")}</td>`)
        .join("");
      const oThaoTac = actions ? `<td><div class="row-actions">${actions(row, index)}</div></td>` : "";
      return `<tr>${cacO}${oThaoTac}</tr>`;
    };

    const soCot = columns.length + (actions ? 1 : 0);
    const dongTrong = `<tr><td colspan="${soCot}"><div class="empty">Chưa có dữ liệu phù hợp. Thử thay bộ lọc hoặc thêm bản ghi mới.</div></td></tr>`;
    const thanBang = rows.length ? rows.map(taoDong).join("") : dongTrong;

    return `<div class="table-wrap"><table><thead><tr>${tieuDe}</tr></thead><tbody>${thanBang}</tbody></table></div>`;
  }

  function taoPhanTrang(data) {
    const tongSoTrang = data.totalPages || 1;
    return [
      `<div class="pager">`,
      `<span class="count">${data.total ?? data.items.length} bản ghi</span>`,
      `<button class="button secondary small" data-action="prev" ${page <= 1 ? "disabled" : ""}>← Trước</button>`,
      `<span>Trang ${page} / ${tongSoTrang}</span>`,
      `<button class="button secondary small" data-action="next" ${page >= tongSoTrang ? "disabled" : ""}>Sau →</button>`,
      `</div>`,
    ].join("");
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
        html = [
          `<div class="panel">`,
          `<h2>Đổi mật khẩu</h2>`,
          `<p class="hint">Mật khẩu được cập nhật cho tài khoản dùng chung của nhóm trên cả website và ứng dụng quản lý.</p>`,
          btn("Đổi mật khẩu", "password", "", "primary"),
          `</div>`,
        ].join("");
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
            taoBang(cols, d.items, (r, i) => {
              // Chỉ hiện nút Sửa khi danh mục có trường sửa được ngoài cột khóa
              const coTheSua = spec.fields.some((f) => !f.readOnly && !spec.keys.includes(f.name));
              const nutSua = coTheSua ? btn("Sửa", "edit", i) : "";
              const nutNoiDung = section === "services" ? btn("Ảnh & nội dung", "content", r.MaDichVu) : "";
              return nutSua + nutNoiDung + btn("Xóa", "delete", i);
            });
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
            `<p class="hint">Tổng tiền trong kết quả lọc: <b>${money(d.totalAmount)}</b>. ` +
            `Lập hóa đơn từ chi tiết phiếu sửa đã hoàn thành.</p>` +
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

    // Một ô số liệu trên đầu trang.
    // kieu = "dark" cho ô nổi bật; style dùng khi số tiền dài cần chữ nhỏ hơn
    function oSoLieu(nhan, giaTri, ghiChu, kieu = "", style = "") {
      const lop = kieu ? `stat ${kieu}` : "stat";
      const thuocTinhStyle = style ? ` style="${style}"` : "";
      return `<article class="${lop}"><label>${nhan}</label><strong${thuocTinhStyle}>${giaTri}</strong><small>${ghiChu}</small></article>`;
    }

    // Thu ngân/quản lý thấy lịch chờ và doanh thu; thợ thấy số xe của mình
    const oThu3 = canReceive
      ? oSoLieu("LỊCH CHỜ XÁC NHẬN", d.pendingBookings, "Yêu cầu từ website")
      : oSoLieu("ĐÃ HOÀN THÀNH", count("Hoàn thành"), "Xe được giao phụ trách");
    const oThu4 = canReceive
      ? oSoLieu("THU HÔM NAY", money(d.todayRevenue), "Từ hóa đơn đã thanh toán", "", "font-size:25px")
      : oSoLieu("TỔNG PHIẾU", d.jobs.reduce((s, x) => s + x.count, 0), "Trong phạm vi của bạn", "", "font-size:25px");

    const loiChao = [
      `<div class="welcome">`,
      `<div><h2>Mọi hoạt động, trong một nơi.</h2><p class="muted">Theo dõi cửa hàng và tiếp tục công việc hôm nay.</p></div>`,
      canReceive ? btn("+ Tiếp nhận xe", "create-job", "", "primary") : "",
      `</div>`,
    ].join("");

    const soLieu = [
      `<div class="stats">`,
      oSoLieu("XE ĐANG SỬA", count("Đang sửa"), "Đang được thợ xử lý", "dark"),
      oSoLieu("CHỜ SỬA", count("Chờ sửa"), "Đã tiếp nhận tại cửa hàng"),
      oThu3,
      oThu4,
      `</div>`,
    ].join("");

    const bangTrangThai = taoBang(
      [
        ["status", "Tiến độ", badge],
        ["count", "Số phiếu"],
      ],
      d.jobs,
    );
    const khungTrangThai = `<section class="panel"><div class="panel-head"><h2>Trạng thái phiếu sửa</h2></div>${bangTrangThai}</section>`;

    // Chỉ quản lý mới có d.stockAlerts
    let khungCanhBaoKho = "";
    if (d.stockAlerts) {
      const bangCanhBao = taoBang(
        [
          ["id", "Mã"],
          ["name", "Phụ tùng"],
          ["stock", "Tồn hiện tại"],
          ["expiry", "Hạn sử dụng", date],
        ],
        d.stockAlerts,
      );
      khungCanhBaoKho = [
        `<section class="panel">`,
        `<div class="panel-head"><h2>Phụ tùng cần chú ý</h2><a href="#parts">Đến kho →</a></div>`,
        bangCanhBao,
        `<p class="hint">Tồn không quá 5 hoặc hết hạn trong 30 ngày tới.</p>`,
        `</section>`,
      ].join("");
    }

    return loiChao + soLieu + khungTrangThai + khungCanhBaoKho;
  }
  async function hienBaoCao() {
    if (!from && !to) {
      from = today().slice(0, 8) + "01";
      to = today();
    }
    const d = await api("/api/workshop/reports?" + layBoLoc());
    const max = Math.max(1, ...d.daily.map((x) => x.amount));

    const duongDanCsv = `/api/workshop/reports/export?${new URLSearchParams({ from, to })}`;
    const boLoc = taoBoLoc({
      dates: true,
      extra: `<a class="button primary" href="${duongDanCsv}">Tải CSV</a>`,
    });

    const oSoLieu = (nhan, soTien, ghiChu, kieu = "stat") =>
      `<article class="${kieu}"><label>${nhan}</label><strong style="font-size:24px">${money(soTien)}</strong><small>${ghiChu}</small></article>`;

    const soLieu = [
      `<div class="stats">`,
      oSoLieu("DOANH THU ĐÃ THU", d.paid, "Theo ngày thanh toán", "stat dark"),
      oSoLieu("GIÁ TRỊ NHẬP KHO", d.purchases, "Theo ngày nhập"),
      oSoLieu("THU TRỪ NHẬP KHO", d.cashDifference, "Không phải lợi nhuận kế toán"),
      oSoLieu("HOÀN THÀNH CHƯA THU", d.unpaid, "Tất cả thời gian"),
      `</div>`,
    ].join("");

    // Biểu đồ cột ngang: độ dài mỗi cột tỉ lệ với ngày có doanh thu cao nhất
    const taoCot = (x) => {
      const doDai = Math.max(1, (x.amount / max) * 100);
      return [
        `<div class="chart-row">`,
        `<span>${date(x.date)}</span>`,
        `<div class="chart-track"><span style="width:${doDai}%"></span></div>`,
        `<strong>${money(x.amount)}</strong>`,
        `</div>`,
      ].join("");
    };
    const bieuDo = d.daily.length
      ? d.daily.map(taoCot).join("")
      : '<div class="empty">Chưa có khoản thu trong kỳ.</div>';

    const bangDichVu = taoBang(
      [
        ["name", "Dịch vụ"],
        ["quantity", "Lượt"],
        ["amount", "Doanh thu", money],
      ],
      d.services,
    );

    const haiCot = [
      `<div class="grid-two">`,
      `<section class="panel"><h2>Doanh thu theo ngày</h2><div class="chart-bars">${bieuDo}</div></section>`,
      `<section class="panel"><h2>Dịch vụ được sử dụng</h2>${bangDichVu}</section>`,
      `</div>`,
    ].join("");

    return boLoc + soLieu + haiCot;
  }
  function layKhoa(spec, row) {
    return spec.keys
      .map((k) => {
        const f = spec.fields.find((x) => x.name === k);
        return f.type === "date" ? String(row[k]).slice(0, 10) : row[k];
      })
      .join("~");
  }
  const modelHint = 'list="motorcycle-models" placeholder="Ví dụ: Vision"';
  const brandHint = 'list="motorcycle-brands" placeholder="Ví dụ: Honda"';
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
          if (f.type === "textarea") {
            const oNhap = `<textarea name="${f.name}" rows="3" maxlength="${f.max}" required>${esc(v)}</textarea>`;
            return `<label class="field full"><span>${esc(f.label)}</span>${oNhap}</label>`;
          }
          if (f.type === "bool") {
            const oNhap = `<input name="${f.name}" type="checkbox" ${v ? "checked" : ""}/>`;
            return `<label class="field"><span>${esc(f.label)}</span>${oNhap}</label>`;
          }
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
          const hint = { LoaiXe: modelHint, HangSanXuat: brandHint }[f.name] ?? "";
          return field(
            f.name,
            f.label,
            type,
            value,
            `${fixed ? "readonly" : ""} ${!f.optional ? "required" : ""} maxlength="${f.max}" ${bounds} ${hint}`,
          );
        }),
    );
    const ghiChuPhuTung =
      section === "parts"
        ? '<p class="hint">Phụ tùng mới có tồn 0. Dùng Phiếu nhập để tăng tồn hoặc Điều chỉnh tồn khi kiểm kê.</p>'
        : "";
    moHopThoai(
      (row ? "Cập nhật · " : "Thêm · ") + spec.title,
      `<div class="form-grid">${fields.join("")}</div>${ghiChuPhuTung}`,
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
  // Nút "Kiểm tra tiền và phụ tùng" và vùng hiện kết quả xem trước, dùng trong form tiếp nhận.
  const xemTruocPhieuMarkup =
    `<div class="detail-actions"><button class="button secondary" type="button" id="job-preview-button">Kiểm tra tiền và phụ tùng</button></div>
    <div id="job-preview" aria-live="polite"></div>`;

  // Dòng dịch vụ hợp lệ: đã chọn dịch vụ và số lượng là số nguyên 1–1000.
  const dongDichVuHopLe = (line) =>
    line.serviceId && Number.isInteger(line.quantity) && line.quantity >= 1 && line.quantity <= 1000;

  function ganXemTruocPhieu(layDuLieu) {
    const button = body.querySelector("#job-preview-button");
    const output = body.querySelector("#job-preview");
    button.addEventListener("click", async () => {
      const input = layDuLieu();
      const duDuLieu = input.vehicleType && input.services.length && input.services.every(dongDichVuHopLe);
      if (!duDuLieu) {
        output.textContent = "Nhập mẫu xe, chọn dịch vụ và số lượng hợp lệ để kiểm tra.";
        return;
      }
      const request = JSON.stringify(input);
      const conHienTai = () => button.isConnected && dialog.open && request === JSON.stringify(layDuLieu());
      button.disabled = true;
      output.textContent = "Đang kiểm tra…";
      try {
        const result = await api("/api/workshop/jobs/preview", { method: "POST", body: request });
        if (!conHienTai()) return;
        const parts = result.parts.map((part) => ({ ...part, issue: part.issue || "Đủ" }));
        output.innerHTML = `<div class="invoice-total">Tạm tính <b>${money(result.total)}</b></div>${parts.length ? taoBang([
          ["id", "Mã phụ tùng"],
          ["name", "Phụ tùng"],
          ["quantity", "Cần dùng"],
          ["stock", "Tồn kho"],
          ["issue", "Kiểm tra"],
        ], parts) : '<p class="hint">Các dịch vụ đã chọn không có định mức phụ tùng.</p>'}
          <p class="hint">Kho và giá được kiểm tra lại khi lưu phiếu.</p>`;
      } catch (e) {
        if (conHienTai()) output.textContent = e.message;
      } finally {
        button.disabled = false;
      }
    });
    return () => output.replaceChildren();
  }

  async function tiepNhanXe() {
    const [serviceOptions, mechanics] = await Promise.all([
      options("services"),
      options("mechanics"),
    ]);
    const layDuLieu = () => ({
      vehicleType: form.elements.vehicleType.value.trim(),
      services: [...body.querySelectorAll("#lines .line-row")].map((row) => ({
        serviceId: row.querySelector("select").value,
        quantity: Number(row.querySelector("input").value),
      })),
    });
    const cacTruong = [
      field("customerName", "Họ tên khách hàng", "text", "", 'required maxlength="100"'),
      field("phone", "Số điện thoại", "tel", "", 'required pattern="0[0-9]{8,10}" maxlength="11"'),
      field("licensePlate", "Biển số xe", "text", "", 'required maxlength="20"'),
      field("vehicleType", "Mẫu xe", "text", "", `required maxlength="50" ${modelHint}`),
      field("brand", "Hãng xe", "text", "", `required maxlength="50" ${brandHint}`),
      field("color", "Màu sắc", "text", "", 'required maxlength="30"'),
      field("dueDate", "Ngày hẹn trả", "date", today(), `required min="${today()}"`),
      selectField("mechanicId", "Phân công thợ (có thể bổ sung sau)", mechanics, ""),
    ].join("");

    const noiDungForm = [
      `<div class="form-grid">${cacTruong}</div>`,
      `<h3 style="margin-top:24px">Dịch vụ thực hiện</h3>`,
      `<div id="job-services"><div class="line-items" id="lines"></div>${btn("+ Thêm dịch vụ", "line-add")}</div>`,
      xemTruocPhieuMarkup,
    ].join("");

    moHopThoai(
      "Tiếp nhận xe",
      noiDungForm,
      async (d) => {
        const input = Object.fromEntries(d);
        Object.assign(input, layDuLieu());
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
    const xoaXemTruoc = ganXemTruocPhieu(layDuLieu);
    form.elements.vehicleType.addEventListener("input", xoaXemTruoc);
    const serviceFields = body.querySelector("#job-services");
    serviceFields.addEventListener("input", xoaXemTruoc);
    serviceFields.addEventListener("click", (e) => {
      if (e.target.closest('[data-action="line-add"], [data-action="line-remove"]')) xoaXemTruoc();
    });
  }
  // Thêm một dòng (chọn dịch vụ/phụ tùng + số lượng) vào form đang mở.
  // Dùng chung cho phiếu nhập kho (Người 4) và tiếp nhận xe (Người 3).
  function themDong() {
    const danhSachDong = document.querySelector("#lines");
    // Mỗi form tự khai báo số lượng tối đa qua data-max; mặc định 1.000
    const soLuongToiDa = Number(danhSachDong.dataset.max) || 1000;

    const dongMoi = `
      <div class="line-row">
        <select required aria-label="Dịch vụ hoặc phụ tùng">${body.dataset.lineOptions}</select>
        <input aria-label="Số lượng" type="number" value="1" min="1" max="${soLuongToiDa}" step="1" required />
        <button type="button" data-action="line-remove" aria-label="Xóa dòng">×</button>
      </div>`;

    danhSachDong.insertAdjacentHTML("beforeend", dongMoi);
  }

  // Người 4: form lập phiếu nhập kho nhiều dòng.
  async function nhapKho() {
    const luaChonPhuTung = await options("parts");

    const noiDungForm = `
      <div class="form-grid">
        ${field("date", "Ngày nhập", "date", today(), `required max="${today()}"`)}
        ${field("note", "Ghi chú", "text", "", 'maxlength="255"')}
      </div>
      <h3 style="margin-top:24px">Phụ tùng nhập</h3>
      <div class="line-items" id="lines" data-max="100000"></div>
      ${btn("+ Thêm phụ tùng", "line-add")}
      <p class="hint">
        Đơn giá lấy từ lô phụ tùng. Tạo mã lô mới khi giá nhập hoặc hạn sử dụng thay đổi.
        Phiếu đã ghi sổ giữ nguyên; sai lệch được xử lý bằng điều chỉnh tồn có lý do.
      </p>`;

    async function luuPhieuNhap(duLieuForm) {
      const phieuNhap = Object.fromEntries(duLieuForm);

      // Mỗi dòng trên form thành một phụ tùng trong phiếu
      phieuNhap.lines = [...body.querySelectorAll(".line-row")].map((dong) => ({
        partId: dong.querySelector("select").value,
        quantity: Number(dong.querySelector("input").value),
      }));

      const ketQua = await api("/api/workshop/stock/purchases", {
        method: "POST",
        body: JSON.stringify(phieuNhap),
      });
      return ketQua.message;
    }

    moHopThoai("Lập phiếu nhập kho", noiDungForm, luuPhieuNhap, "Lưu phiếu và cộng kho");

    body.dataset.lineOptions = luaChonPhuTung;
    themDong();
  }

  // Người 4: xem chi tiết một phiếu nhập kèm tổng tiền.
  async function xemPhieuNhap(maPhieuNhap) {
    const duLieu = await api("/api/workshop/stock/purchases/" + encodeURIComponent(maPhieuNhap));

    let tongTienNhap = 0;
    for (const dong of duLieu.lines) {
      tongTienNhap += Number(dong.amount);
    }

    const bangChiTiet = taoBang(
      [
        ["partId", "Mã phụ tùng"],
        ["name", "Tên"],
        ["quantity", "Số lượng"],
        ["price", "Đơn giá nhập", money],
        ["amount", "Thành tiền", money],
      ],
      duLieu.lines,
    );

    moHopThoai(
      "Chi tiết phiếu nhập " + maPhieuNhap,
      bangChiTiet + `<p class="hint"><strong>Tổng nhập: ${money(tongTienNhap)}</strong></p>`,
    );
  }

  // Người 4: kiểm kê, nhập số tồn đếm thực tế và lý do điều chỉnh.
  async function kiemKeTon() {
    const luaChonPhuTung = await options("parts");

    const noiDungForm = `
      <div class="form-grid">
        ${selectField("partId", "Phụ tùng", luaChonPhuTung)}
        ${field("stock", "Số tồn thực tế", "number", "", 'required min="0" max="1000000" step="1"')}
        ${field("reason", "Lý do điều chỉnh", "text", "", 'required minlength="5" maxlength="255"')}
      </div>`;

    async function luuKiemKe(duLieuForm) {
      const kiemKe = Object.fromEntries(duLieuForm);
      kiemKe.stock = Number(kiemKe.stock);

      const ketQua = await api("/api/workshop/stock/adjust", {
        method: "POST",
        body: JSON.stringify(kiemKe),
      });
      return ketQua.message;
    }

    moHopThoai("Điều chỉnh tồn sau kiểm kê", noiDungForm, luuKiemKe);
  }
  async function xemPhieu(id, invoiceOnly = false) {
    const d = await api("/api/workshop/jobs/" + encodeURIComponent(id)),
      j = d.job;
    // Phần đầu hóa đơn (chỉ khi xem/in hóa đơn)
    const dauHoaDon = invoiceOnly
      ? [
          `<div class="invoice-heading">`,
          `<span class="eyebrow">CỬA HÀNG SỬA XE · DỊCH VỤ SỬA XE</span>`,
          `<h2>HÓA ĐƠN THANH TOÁN</h2>`,
          `<p>${esc(d.invoice?.id || "")}</p>`,
          `</div>`,
        ].join("")
      : "";

    const thongTinPhieu = [
      ["Mã phiếu", j.id],
      ["Khách hàng", j.customerName],
      ["Số điện thoại", j.phone],
      ["Biển số", j.licensePlate],
      ["Ngày nhận", date(j.createdOn)],
      ["Hẹn trả", date(j.dueDate)],
    ]
      .map(([k, v]) => `<div><label>${k}</label><b>${esc(v)}</b></div>`)
      .join("");

    const bangDichVu = taoBang(
      [
        ["serviceId", "Mã dịch vụ"],
        ["name", "Tên dịch vụ"],
        ["quantity", "Số lượng"],
        ["amount", "Thành tiền", money],
      ],
      d.lines,
    );

    let html =
      dauHoaDon +
      `<div class="detail-grid">${thongTinPhieu}</div>` +
      bangDichVu +
      `<div class="invoice-total">Tổng cộng: <b>${money(j.total)}</b></div>`;

    if (invoiceOnly) {
      html += [
        `<p>Ngày thanh toán: <b>${date(d.invoice?.paidOn)}</b></p>`,
        `<p class="hint">Hóa đơn nội bộ phục vụ bài tập, không phải hóa đơn điện tử thuế.</p>`,
        `<div class="detail-actions no-print">${btn("In hóa đơn", "print", "", "primary")}</div>`,
      ].join("");
      moHopThoai("Hóa đơn · " + (d.invoice?.id || j.id), html);
      return;
    }

    // Bảng phân công: chỉ bỏ phân công được khi phiếu còn Chờ sửa
    const ngayGio = (v) => `${date(v)} ${esc(String(v).slice(11, 16))}`;
    const nutBoPhanCong =
      canReceive && j.status === "Chờ sửa"
        ? (r) => btn("Bỏ phân công", "unassign", j.id + "~" + r.employeeId)
        : null;
    const bangPhanCong = taoBang(
      [
        ["name", "Nhân viên"],
        ["start", "Bắt đầu", ngayGio],
        ["end", "Dự kiến xong", ngayGio],
      ],
      d.assignments,
      nutBoPhanCong,
    );

    // Các nút thao tác theo trạng thái phiếu và quyền
    const dangXuLy = ["Chờ sửa", "Đang sửa"].includes(j.status);
    const cacNut = [
      canReceive && dangXuLy ? btn("Phân công / đổi giờ", "assign", id) + btn("Sửa ngày hẹn", "due-date", id) : "",
      j.status === "Chờ sửa" ? btn("Bắt đầu sửa", "status", id + "~Đang sửa", "primary") : "",
      j.status === "Đang sửa" ? btn("Hoàn thành", "status", id + "~Hoàn thành", "primary") : "",
      canReceive && j.status === "Chờ sửa" ? btn("Hủy và hoàn kho", "status", id + "~Đã hủy", "danger") : "",
      canReceive && j.status === "Hoàn thành" && !j.paid ? btn("Lập hóa đơn / Thu tiền", "payment", id, "primary") : "",
      canReceive && j.paid ? btn("Xem / In hóa đơn", "print-invoice", id) : "",
    ].join("");

    html += `<h3>Phân công thợ</h3>${bangPhanCong}<div class="detail-actions">${cacNut}</div>`;
    moHopThoai("Phiếu " + id + " · " + j.status, html);
  }
  async function nhanLich(id) {
    const booking = current.find((row) => row.id === id);
    if (!booking) throw new Error("Lịch hẹn không còn trong danh sách. Hãy tải lại.");
    const mechanics = await options("mechanics");
    const cacTruong = [
      field("dueDate", "Ngày hẹn trả", "date", today(), `required min="${today()}"`),
      field("color", "Màu xe", "text", "", 'required maxlength="30"'),
      selectField("mechanicId", "Thợ phụ trách", mechanics, ""),
    ].join("");

    const noiDungForm = [
      `<p>Xe <b>${esc(booking.brand)} ${esc(booking.vehicleType)}</b> · ${esc(booking.licensePlate)}</p>`,
      `<p>Dịch vụ <b>${esc(booking.service)}</b></p>`,
      `<div class="form-grid">${cacTruong}</div>`,
      xemTruocPhieuMarkup,
    ].join("");

    moHopThoai(
      "Tiếp nhận lịch " + id,
      noiDungForm,
      async (d) =>
        (
          await api(
            "/api/workshop/bookings/" + encodeURIComponent(id) + "/accept",
            { method: "POST", body: JSON.stringify(Object.fromEntries(d)) },
          )
        ).message,
      "Tiếp nhận và trừ kho",
    );
    ganXemTruocPhieu(() => ({
      vehicleType: booking.vehicleType,
      services: [{ serviceId: booking.serviceId, quantity: 1 }],
    }));
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
    const ghiChuAnh = d.imageUrl ? "Đang dùng ảnh đã lưu." : "Chưa có ảnh riêng.";
    const thanhCongCu = btn("B · Đậm", "bold") + btn("I · Nghiêng", "italic") + btn("• Danh sách", "list");

    const noiDungForm = [
      // Chọn ảnh → tải lên ngay, đường dẫn ảnh lưu vào ô ẩn imageUrl
      `<label class="field"><span>Ảnh JPG/PNG/WebP, tối đa 2 MB</span><input type="file" id="service-image" accept="image/jpeg,image/png,image/webp"/></label>`,
      `<input type="hidden" name="imageUrl" value="${esc(d.imageUrl)}"/>`,
      `<div id="image-note" class="hint">${ghiChuAnh}</div>`,
      // Editor: vùng contenteditable, khi lưu nội dung được lọc HTML rồi chép vào textarea ẩn
      `<div class="editor-tools">${thanhCongCu}</div>`,
      `<textarea id="mo-ta-html" name="description" hidden></textarea>`,
      `<div contenteditable="true" role="textbox" aria-label="Nội dung dịch vụ" class="content-editor" id="rich-editor">${locNoiDung(d.description)}</div>`,
      `<p class="hint">Nội dung hiển thị công khai ở trang dịch vụ. Chỉ giữ các định dạng văn bản an toàn.</p>`,
    ].join("");

    moHopThoai(
      "Ảnh và nội dung dịch vụ",
      noiDungForm,
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
      const cacTruong = [
        selectField("employeeId", "Thợ phụ trách", choices),
        field("start", "Bắt đầu", "datetime-local", today() + "T08:00", "required"),
        field("end", "Kết thúc dự kiến", "datetime-local", today() + "T17:00", "required"),
      ].join("");
      return moHopThoai(
        "Phân công thợ",
        `<div class="form-grid">${cacTruong}</div>` +
          `<p class="hint">Thợ chính tối đa 3 xe đang xử lý, thợ phụ tối đa 2 xe. Chọn lại cùng thợ để cập nhật thời gian.</p>`,
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
      // Ngày thu: từ ngày lập phiếu đến hôm nay
      const ngayLapPhieu = String(d.job.createdOn).slice(0, 10);
      const noiDungForm = [
        `<p>Khách hàng: <b>${esc(d.job.customerName)}</b></p>`,
        `<p>Tổng tiền cần thu: <b>${money(d.job.total)}</b></p>`,
        field("paidOn", "Ngày thanh toán", "date", today(), `required max="${today()}" min="${ngayLapPhieu}"`),
        `<p class="hint">Xác nhận khi đã nhận đủ tiền. Tổng tiền lấy từ chi tiết phiếu, không nhập lại thủ công.</p>`,
      ].join("");
      return moHopThoai(
        "Thu tiền phiếu " + id,
        noiDungForm,
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
    if (action === "purchase-detail") return xemPhieuNhap(id);
    if (action === "stock-adjust") return kiemKeTon();
    if (action === "password")
      return moHopThoai(
        "Đổi mật khẩu",
        `<div class="form-grid">` +
          field("oldPassword", "Mật khẩu hiện tại", "password", "", 'required autocomplete="current-password"') +
          field("newPassword", "Mật khẩu mới", "password", "", 'required minlength="6" maxlength="100" autocomplete="new-password"') +
          `</div>`,
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
