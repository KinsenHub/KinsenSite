import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import { html, customElement } from "@umbraco-cms/backoffice/external/lit";

@customElement("car-export-dashboard")
export class CarExportDashboard extends UmbControllerBase {
  render() {
    return html`
      <uui-box headline="Export Αυτοκινήτων">
        <p>Κατεβάστε όλα τα cars σε CSV:</p>
        <uui-button
          label="Export Cars"
          look="primary"
          @click=${() =>
            window.open("/umbraco/api/CarExport/ExportCars", "_blank")}
        >
          📥 Export Cars
        </uui-button>
      </uui-box>
    `;
  }
}
