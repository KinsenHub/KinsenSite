import { html, css, LitElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";

class FeatureCardPreview extends UmbElementMixin(LitElement) {
  static properties = { content: { attribute: false } };

  static styles = css`
    :host {
      display: block;
    }
    .wrap {
      padding: 10px;
      font-family: sans-serif;
    }
    .title {
      font-weight: 600;
      font-size: 16px;
      line-height: 1.2;
      color: #2b2b2b;
    }
    .subtitle {
      font-size: 13px;
      color: #666;
    }
  `;

  render() {
    // ✅ Πάρε το περιεχόμενο από την κάρτα
    const d = this.content ?? {};
    console.log("📦 FeatureCard content:", d);

    // ✅ Αν description είναι richtext object → πάρε markup
    const description =
      typeof d.description === "string"
        ? d.description
        : d.description?.markup || "Χωρίς περιγραφή";

    const title = description.replace(/<[^>]+>/g, "").substring(0, 50);
    const subtitle = d.targetSlug ? `Slug: ${d.targetSlug}` : "";

    return html`
      <div class="wrap">
        <div class="title">${title}</div>
        ${subtitle ? html`<div class="subtitle">${subtitle}</div>` : null}
      </div>
    `;
  }
}

if (!customElements.get("feature-card-preview")) {
  customElements.define("feature-card-preview", FeatureCardPreview);
}

export default FeatureCardPreview;
