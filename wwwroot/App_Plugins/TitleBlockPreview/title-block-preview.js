import { html, css, LitElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";

class TitleBlockPreview extends UmbElementMixin(LitElement) {
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
      font-size: 18px;
      line-height: 1.2;
      color: #333;
    }
    .subtitle {
      font-size: 14px;
      color: #666;
    }
  `;

  render() {
    const d = this.content ?? {};
    const title = d.titleCard || d.accordionTitle || "Χωρίς τίτλο";
    const subtitle =
      d.cardBody || d.accordionContent
        ? (typeof (d.cardBody || d.accordionContent) === "string"
            ? d.cardBody || d.accordionContent
            : d.cardBody?.markup || d.accordionContent?.markup || ""
          )
            .replace(/<[^>]+>/g, "")
            .substring(0, 50)
        : "";

    return html`
      <div class="wrap">
        <div class="title">${title}</div>
        ${subtitle ? html`<div class="subtitle">${subtitle}…</div>` : null}
      </div>
    `;
  }
}

if (!customElements.get("title-block-preview")) {
  customElements.define("title-block-preview", TitleBlockPreview);
}

export default TitleBlockPreview;
