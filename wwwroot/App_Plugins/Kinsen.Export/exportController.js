(function () {
  "use strict";

  function ExportController($scope, $http, editorState) {
    var vm = this;

    var current =
      editorState && editorState.current ? editorState.current : null;
    vm.id = current && current.id ? current.id : null;
    vm.key = current && current.key ? current.key : null; // GUID του node
    vm.name = current && current.name ? current.name : "";
    vm.mode = "append"; // "append" | "replace"
    vm.out = "";

    // Export CSV
    vm.export = function () {
      if (!vm.key) {
        vm.out = "Δεν βρέθηκε node key.";
        return;
      }
      var url =
        "/umbraco/backoffice/Kinsen/CarBlock/ExportCsv?id=" +
        encodeURIComponent(vm.key);
      window.open(url, "_blank");
    };

    // Import CSV (multipart)
    vm.import = function () {
      var fileInput = document.getElementById("file");
      var f = fileInput ? fileInput.files[0] : null;
      if (!f) {
        vm.out = "Διάλεξε CSV αρχείο.";
        return;
      }
      if (!vm.key) {
        vm.out = "Δεν βρέθηκε node key.";
        return;
      }

      var form = new FormData();
      form.append("file", f);

      var url =
        "/umbraco/backoffice/Kinsen/CarBlock/ImportCsv" +
        "?id=" +
        encodeURIComponent(vm.key) +
        "&mode=" +
        encodeURIComponent(vm.mode);

      $http
        .post(url, form, {
          withCredentials: true,
          headers: { "Content-Type": undefined }, // αφήνουμε τον browser να βάλει boundary
          transformRequest: angular.identity,
        })
        .then(function (res) {
          // μπορεί να επιστρέφεις JSON ή απλό string — δείξε και τα δύο όμορφα
          vm.out =
            "HTTP " +
            res.status +
            "\n\n" +
            (typeof res.data === "string"
              ? res.data
              : JSON.stringify(res.data, null, 2));
        })
        .catch(function (err) {
          var msg =
            err && err.data
              ? typeof err.data === "string"
                ? err.data
                : JSON.stringify(err.data)
              : (err && err.message) || "Σφάλμα";
          vm.out = "Error: " + msg;
        });
    };
  }

  angular
    .module("umbraco")
    .controller("Kinsen.ExportController", [
      "$scope",
      "$http",
      "editorState",
      ExportController,
    ]);
})();
