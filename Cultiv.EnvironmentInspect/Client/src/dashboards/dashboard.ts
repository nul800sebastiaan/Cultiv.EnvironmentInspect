import { LitElement, css, html, customElement, property, repeat, when, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { EnvironmentVariable } from "../api/types.gen";
import { CultivEnvironmentInspectService } from "../api/sdk.gen";

@customElement('environmentinspect-dashboard')
export class EnvironmentInspectDashboardElement extends UmbElementMixin(LitElement) {

  @property({ type: Array }) environmentVariables: EnvironmentVariable[] = [];
  @property({ type: Boolean }) isLoading: boolean = true;
  @state() displayedCount: number = 50;
  private readonly incrementSize: number = 50;

  get visibleVariables(): EnvironmentVariable[] {
    return this.environmentVariables.slice(0, this.displayedCount);
  }

  get hasMore(): boolean {
    return this.displayedCount < this.environmentVariables.length;
  }

  render() {
    return html`
      ${when(this.isLoading, 
        () => html`
          <div class="loader-container">
            <uui-loader></uui-loader>
          </div>
        `,
        () => html`
          <div class="content-container" @scroll=${this.handleScroll}>          
            <uui-table>
              <uui-table-column style="width: 50%;"></uui-table-column>
              <uui-table-head style="background-color: #1b264f; color: white">
                <uui-table-head-cell>Environment value path</uui-table-head-cell>
                <uui-table-head-cell>Value / Provider</uui-table-head-cell>
              </uui-table-head>
              ${when(this.visibleVariables.length, () => html`
                ${repeat(this.visibleVariables,
                  (item) => item.key,
                  (item) => html`
                    <uui-table-row>
                      <uui-table-cell clip-text="">${item.key}</uui-table-cell>
                      <uui-table-cell clip-text="">
                        <em style="font-size: 0.8em">${item.provider}</em>
                        <br/>
                        ${item.value}
                      </uui-table-cell>
                    </uui-table-row>
                `)}
              `)}
            </uui-table>
          </div>
          <div class="counter-container">
            Showing ${this.visibleVariables.length} of ${this.environmentVariables.length} items
            ${when(this.hasMore, () => html`
              <uui-button 
                look="primary" 
                label="Load all"
                @click=${this.loadAll}
                style="margin-left: 1rem;">
                Load all
              </uui-button>
            `)}
          </div>
        `
      )}
    `;
  }

  handleScroll(event: Event) {
    const target = event.target as HTMLElement;
    const scrollThreshold = 200; // pixels from bottom to trigger load
    
    if (target.scrollHeight - target.scrollTop - target.clientHeight < scrollThreshold) {
      if (this.hasMore) {
        this.loadMore();
      }
    }
  }

  loadMore() {
    this.displayedCount = Math.min(
      this.displayedCount + this.incrementSize,
      this.environmentVariables.length
    );
  }

  loadAll() {
    this.displayedCount = this.environmentVariables.length;
  }

  async firstUpdated() {
    try {
      const { data } = await this.getData();
      if (data) {
        this.environmentVariables = data;
      }
    } catch (e) {
      // Optionally handle error
      console.error("Failed to load environment variables", e);
    } finally {
      this.isLoading = false;
    }
  }

  async getData(): Promise<any> { // or a more specific type if available
    return CultivEnvironmentInspectService.getEnvironment();
  }

  static styles = [
    css`
      h4 {
        font-size: var(--uui-type-h4-size);
      }

      .loader-container {
        display: flex;
        justify-content: center;
        align-items: center;
        padding: 4rem;
      }

      .content-container {
        max-height: calc(100vh - 180px);
        overflow-y: auto;
      }

      uui-box {
        margin-bottom: 1rem;
      }

      .load-more-container {
        display: flex;
        justify-content: center;
        padding: 2rem 0;
      }

      .counter-container {
        display: flex;
        align-items: center;
        justify-content: center;
        text-align: center;
        padding: 0.75rem 1rem 1rem 1rem;
        color: var(--uui-color-text-alt);
        font-size: 0.9em;
        background-color: var(--uui-color-surface);
        border-top: 1px solid var(--uui-color-border);
      }
    `,
  ];
}

export default EnvironmentInspectDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    'environmentinspect-dashboard': EnvironmentInspectDashboardElement;
  }
}