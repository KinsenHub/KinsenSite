// Backoffice v14+ web component dashboard
// Εμφανίζει την UploadPage του controller μέσα σε iframe

class KinsenImportDashboard extends HTMLElement {
  connectedCallback() {
    // Μπορείς να βάλεις default parent key εδώ αν θες:
    const defaultParent = ""; // π.χ. 'ff66cefb-4f34-4211-9adb-ea698be7b7b3'
    const url = "/umbraco/backoffice/Kinsen/CarImport/UploadPage";

    this.style.display = "block";
    this.innerHTML = `
      <div style="padding:16px">
        <h2 style="margin:0 0 12px">CSV Import (Cars)</h2>
        <p style="margin:0 0 16px">
          Διαλέξτε .csv και ορίστε Parent (Id/Key/UDI). Θα δημιουργηθούν/ενημερωθούν τα αυτοκίνητα.
        </p>
        <iframe src="${url}" style="width:100%;height:600px;border:0;background:#fff;border-radius:8px;"></iframe>
      </div>
    `;
  }
}

customElements.define("kinsen-import-dashboard", KinsenImportDashboard);

// Κάνουμε register ένα dashboard "element" για το extension
// Το νέο backoffice βρίσκει το elementName από το pathname
window.umbExtensions = window.umbExtensions || [];
window.umbExtensions.push({
  type: "dashboard",
  alias: "Kinsen.Import.Dashboard",
  name: "Import cars",
  elementName: "kinsen-import-dashboard",
  meta: {
    label: "Import cars",
    pathname: "kinsen-import",
  },
  conditions: [{ alias: "Umb.Condition.SectionAlias", match: "content" }],
});
