document.addEventListener("DOMContentLoaded", function () {
  var newText = "Maintenance: SLA MIT Team";

  document.querySelectorAll("footer").forEach(function (footer) {
    var replaced = false;

    footer.querySelectorAll("p, small, div, span").forEach(function (el) {
      var t = (el.textContent || "").trim();
      if (t.indexOf("Documentation built with") >= 0 && t.indexOf("MkDocs") >= 0) {
        el.textContent = newText;
        replaced = true;
      }
    });

    // 若找不到預設字串（某些頁/版本），就補一行
    if (!replaced) {
      var p = document.createElement("p");
      p.style.margin = "0.5rem 0 0 0";
      p.textContent = newText;
      footer.appendChild(p);
    }
  });
});
