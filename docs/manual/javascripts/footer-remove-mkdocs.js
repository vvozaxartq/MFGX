document.addEventListener("DOMContentLoaded", function () {
  // 移除 footer 內的 "Documentation built with MkDocs." 那段
  document.querySelectorAll("footer").forEach(function (footer) {
    footer.querySelectorAll("p, small, div, span").forEach(function (el) {
      var t = (el.textContent || "").trim();
      if (t.indexOf("Documentation built with") >= 0 && t.indexOf("MkDocs") >= 0) {
        el.remove();
      }
    });
  });
});