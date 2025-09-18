import { html, css, LitElement } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UmbMediaUrlRepository } from "@umbraco-cms/backoffice/media";

class CarBlockPreview extends UmbElementMixin(LitElement) {
  static properties = { content: { attribute: false } };

  static styles = css`
    :host {
      display: block;
    }
    .wrap {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 8px;
      pointer-events: none;
    }
    .thumb {
      width: 128px; //ΕΔΩ ΑΛΛΑΓΗ ΜΕΓΕΘΟΥΣ ΦΩΤΟ
      height: 96px;
      border-radius: 8px;
      overflow: hidden;
      flex: 0 0 auto;
      background: #f2f2f2;
      box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.06) inset;
    }
    .thumb img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      display: block;
    }
    .title {
      font-weight: 600;
      line-height: 1.2;
      font-size: 18px;
    }
    .meta {
      color: #666;
      font-size: 15px;
    }
  `;

  constructor() {
    super();
    this.content = undefined;
    this._imgUrl = "";
    this._urlRepo = new UmbMediaUrlRepository(this); // ✅ ΟΧΙ private field
  }

  async _getUrlFromPickerValue(val) {
    if (!val) return "";
    const first = Array.isArray(val) ? val[0] : val;

    // direct url (αν υπάρχει)
    const direct =
      first?.url ||
      first?.mediaLink ||
      (Array.isArray(first?.urls) ? first.urls[0] : "");
    if (direct) return direct;

    // mediaKey / key / string UDI
    const mediaKey =
      first?.mediaKey || first?.key || (typeof first === "string" ? first : "");

    if (!mediaKey) return "";

    try {
      const { data: items } = await this._urlRepo.requestItems([mediaKey]);
      return items?.[0]?.url || "";
    } catch {
      return "";
    }
  }

  // Κάθε φορά που αλλάζει το content, υπολόγισε thumbnail URL
  async updated(changed) {
    if (changed.has("content")) {
      const d = this.content?.data ?? this.content ?? {};
      // 👉 Αν το alias διαφέρει, άλλαξέ το εδώ
      this._imgUrl = await this._getUrlFromPickerValue(
        d.carPic ?? d.mainImage ?? d.image
      );
      this.requestUpdate();
    }
  }

  render() {
    const d = this.content?.data ?? this.content ?? {};
    const brand = d.maker || d.brand || "";
    const model = d.carModel || d.model || "";
    const year = d.yearRelease || d.year || "";

    return html`
      <div class="wrap">
        <div class="thumb">
          ${this._imgUrl ? html`<img src="${this._imgUrl}" alt="" />` : html``}
        </div>
        <div>
          <div class="title">${brand} ${model}</div>
          ${year ? html`<div class="meta">(${year})</div>` : null}
        </div>
      </div>
    `;
  }
}

if (!customElements.get("car-block-preview")) {
  customElements.define("car-block-preview", CarBlockPreview);
}
export default CarBlockPreview;
