import { LitElement, css, html, customElement, property, repeat, when, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { EnvironmentVariable } from "../api/types.gen";
import { CultivEnvironmentInspectService } from "../api/sdk.gen";

@customElement('environmentinspect-dashboard')
export class EnvironmentInspectDashboardElement extends UmbElementMixin(LitElement) {

  @property({ type: Array }) environmentVariables: EnvironmentVariable[] = [];
  @property({ type: Boolean }) isLoading: boolean = true;
  @state() displayedCount: number = 50;
  @state() excludeEmptyValues: boolean = true;
  @state() onlyRedacted: boolean = false;
  @state() replaceColonWithUnderscore: boolean = false;
  @state() azureWebAppAdvancedCopy: boolean = false; // Set from server
  private readonly incrementSize: number = 50;

  get filteredVariables(): EnvironmentVariable[] {
    let filtered = this.environmentVariables;
    
    // Filter out empty values if enabled
    if (this.excludeEmptyValues) {
      filtered = filtered.filter(v => v.value != null && v.value !== '');
    }
    
    // Filter to only redacted items if enabled
    if (this.onlyRedacted) {
      filtered = filtered.filter(v => v.redactedMode != null && v.redactedMode !== '');
    }
    
    return filtered;
  }

  get visibleVariables(): EnvironmentVariable[] {
    return this.filteredVariables.slice(0, this.displayedCount);
  }

  get hasMore(): boolean {
    return this.displayedCount < this.filteredVariables.length;
  }

  get hasAnyRedactions(): boolean {
    return this.environmentVariables.some(v => v.redactedMode != null && v.redactedMode !== '');
  }
  
  formatKey(key: string): string {
    if (this.replaceColonWithUnderscore) {
      return key.replace(/:/g, '__');
    }
    return key;
  }
  
  async copyToClipboard(text: string) {
    try {
      await navigator.clipboard.writeText(text);
      
      const notificationContext = await this.getContext(UMB_NOTIFICATION_CONTEXT);
      notificationContext?.peek('positive', { 
        data: { 
          headline: 'Copied!',
          message: 'Value copied to clipboard' 
        } 
      });
    } catch (err) {
      console.error('Failed to copy text: ', err);
      
      const notificationContext = await this.getContext(UMB_NOTIFICATION_CONTEXT);
      notificationContext?.peek('danger', { 
        data: { 
          headline: 'Copy Failed',
          message: 'Could not copy to clipboard' 
        } 
      });
    }
  }

  generateAzureWebAppSnippet(key: string, value: string): string {
    const formattedKey = this.formatKey(key);
    return JSON.stringify({
      name: formattedKey,
      value: value,
      slotSetting: false
    }, null, 2);
  }

  getRedactionEmoji(redactedMode?: string | null): string {
    switch (redactedMode) {
      case 'Full':
        return '🔒'; // Full redaction - locked
      case 'Partial':
        return '👁️'; // Partial redaction - eye (partial visibility)
      case 'Advanced':
        return '🔐'; // Advanced redaction - locked with key (custom rules)
      default:
        return ''; // No redaction
    }
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
          <div class="filter-container">
            <div class="toggle-item">
              <uui-toggle
                ?checked=${this.excludeEmptyValues}
                @change=${(e: CustomEvent) => this.excludeEmptyValues = (e.target as any).checked}>
              </uui-toggle>
              <label>Exclude empty values</label>
            </div>
            <div class="toggle-item">
              <uui-toggle
                ?checked=${this.onlyRedacted}
                ?disabled=${!this.hasAnyRedactions}
                title=${this.hasAnyRedactions ? '' : 'No redacted values available'}
                @change=${(e: CustomEvent) => this.onlyRedacted = (e.target as any).checked}>
              </uui-toggle>
              <label>Only redacted</label>
            </div>
            <div class="toggle-item">
              <uui-toggle
                ?checked=${this.replaceColonWithUnderscore}
                @change=${(e: CustomEvent) => this.replaceColonWithUnderscore = (e.target as any).checked}>
              </uui-toggle>
              <label>Environment variable format (__ instead of :)</label>
            </div>
          </div>
          <div class="content-container" @scroll=${this.handleScroll}>          
            <uui-table>
              <uui-table-column style="width: 50%;"></uui-table-column>
              <uui-table-head style="background-color: #1b264f; color: white">
                <uui-table-head-cell>Environment value path</uui-table-head-cell>
                <uui-table-head-cell>Value / Provider</uui-table-head-cell>
                ${when(this.azureWebAppAdvancedCopy, () => html`
                  <uui-table-head-cell style="width: 100px; text-align: center;">Azure</uui-table-head-cell>
                `)}
              </uui-table-head>
              ${when(this.visibleVariables.length, () => html`
                ${repeat(this.visibleVariables,
                  (item) => item.key,
                  (item) => html`
                    <uui-table-row>
                      <uui-table-cell class="key-cell">
                        <div class="key-container">
                          <span class="key-text">${this.formatKey(item.key!)}</span>
                          <uui-button 
                            compact
                            look="secondary"
                            label="Copy key"
                            title="Copy key to clipboard"
                            @click=${() => this.copyToClipboard(this.formatKey(item.key!))}
                            class="copy-button">
                            📋
                          </uui-button>
                        </div>
                      </uui-table-cell>
                      <uui-table-cell class="value-cell">
                        <div class="value-container">
                          <div class="value-content">
                            <em style="font-size: 0.8em">${item.provider}</em>
                            <br/>
                            ${when(item.redactedMode, () => html`
                              <span style="margin-right: 0.25rem;" title="Redacted: ${item.redactedMode}">${this.getRedactionEmoji(item.redactedMode)}</span>
                            `)}
                            <span class="value-text">${item.value}</span>
                          </div>
                          <uui-button 
                            compact
                            look="secondary"
                            label="Copy value"
                            title="Copy value to clipboard"
                            @click=${() => this.copyToClipboard(item.value || '')}
                            class="copy-button">
                            📋
                          </uui-button>
                        </div>
                      </uui-table-cell>
                      ${when(this.azureWebAppAdvancedCopy, () => html`
                        <uui-table-cell class="azure-cell">
                          <uui-button 
                            compact
                            look="primary"
                            label="Copy Azure snippet"
                            title="Copy as Azure Web App JSON snippet"
                            @click=${() => this.copyToClipboard(this.generateAzureWebAppSnippet(item.key!, item.value || ''))}>
                            ☁️
                          </uui-button>
                        </uui-table-cell>
                      `)}
                    </uui-table-row>
                `)}
              `)}
            </uui-table>
          </div>
          <div class="counter-container">
            Showing ${this.visibleVariables.length} of ${this.filteredVariables.length} items
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
      this.filteredVariables.length
    );
  }

  loadAll() {
    this.displayedCount = this.filteredVariables.length;
  }

  async firstUpdated() {
    try {
      const { data } = await this.getData();
      if (data) {
        this.environmentVariables = data.variables || [];
        this.azureWebAppAdvancedCopy = data.azureWebAppAdvancedCopy || false;
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

      .filter-container {
        display: flex;
        gap: 1.5rem;
        padding: 1rem;
        background-color: var(--uui-color-surface);
        border-bottom: 1px solid var(--uui-color-border);
        flex-wrap: wrap;
      }

      .toggle-item {
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }

      .toggle-item label {
        cursor: pointer;
        user-select: none;
      }

      .content-container {
        max-height: calc(100vh - 260px);
        overflow-y: auto;
      }

      uui-box {
        margin-bottom: 1rem;
      }

      .key-container {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        justify-content: space-between;
      }

      .key-text {
        flex: 1;
        word-break: break-word;
      }

      .value-container {
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
        justify-content: space-between;
      }

      .value-content {
        flex: 1;
        word-break: break-word;
      }

      .value-text {
        word-break: break-word;
      }

      .azure-cell {
        text-align: center;
        vertical-align: middle !important;
      }

      .copy-button {
        flex-shrink: 0;
        opacity: 0;
        transition: opacity 0.2s;
      }

      uui-table-row:hover .copy-button {
        opacity: 1;
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

      uui-table-cell.key-cell,
      uui-table-cell.value-cell {
        white-space: normal;
        word-break: break-word;
        overflow-wrap: anywhere;
        vertical-align: top;
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