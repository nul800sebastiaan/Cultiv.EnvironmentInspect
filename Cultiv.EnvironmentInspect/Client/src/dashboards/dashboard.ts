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
  @state() onlyStarred: boolean = false;
  @state() replaceColonWithUnderscore: boolean = false;
  @state() azureWebAppAdvancedCopy: boolean = false; // Set from server
  @state() showAzureColumn: boolean = false; // User preference to show/hide Azure column
  @state() settingsPopoverOpen: boolean = false;
  @state() filterText: string = '';
  @state() starredSettings: Set<string> = new Set();
  private togglingStar: Set<string> = new Set(); // Track in-flight toggle requests
  private readonly incrementSize: number = 50;

  get filteredVariables(): EnvironmentVariable[] {
    let filtered = this.environmentVariables;
    
    // Filter by search text
    if (this.filterText) {
      const searchLower = this.filterText.toLowerCase();
      filtered = filtered.filter(v => 
        v.key?.toLowerCase().includes(searchLower) || 
        v.value?.toLowerCase().includes(searchLower) ||
        v.provider?.toLowerCase().includes(searchLower)
      );
    }
    
    // Filter out empty values if enabled
    if (this.excludeEmptyValues) {
      filtered = filtered.filter(v => v.value != null && v.value !== '');
    }
    
    // Filter to only redacted items if enabled
    if (this.onlyRedacted) {
      filtered = filtered.filter(v => v.redactedMode != null && v.redactedMode !== '');
    }
    
    // Filter to only starred items if enabled
    if (this.onlyStarred) {
      filtered = filtered.filter(v => this.starredSettings.has(v.key!));
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
  
  get hasAnyStarred(): boolean {
    return this.starredSettings.size > 0;
  }
  
  isStarred(key: string): boolean {
    return this.starredSettings.has(key);
  }
  
  async toggleStar(key: string) {
    // Prevent concurrent toggles for the same key
    if (this.togglingStar.has(key)) {
      return;
    }
    
    this.togglingStar.add(key);
    
    try {
      const { error } = await CultivEnvironmentInspectService.toggleStar({
        body: { settingKey: key }
      });
      
      if (error) {
        console.error('API error response:', error);
        throw new Error(`Failed to toggle star: ${JSON.stringify(error)}`);
      }
      
      // Update local state
      if (this.starredSettings.has(key)) {
        this.starredSettings.delete(key);
      } else {
        this.starredSettings.add(key);
      }
      
      // Trigger re-render
      this.requestUpdate();
    } catch (err) {
      console.error('Failed to toggle star: ', err);
      
      const notificationContext = await this.getContext(UMB_NOTIFICATION_CONTEXT);
      notificationContext?.peek('danger', { 
        data: { 
          headline: 'Star Toggle Failed',
          message: 'Could not update starred setting' 
        } 
      });
    } finally {
      // Always remove from in-flight set
      this.togglingStar.delete(key);
    }
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

  closeSettingsPopover() {
    this.settingsPopoverOpen = false;
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
            <div class="settings-section">
              <uui-popover 
                id="settings-popover" 
                ?open=${this.settingsPopoverOpen} 
                placement="bottom-start"
                @close=${() => this.closeSettingsPopover()}>
                <uui-button
                  slot="trigger"
                  look="outline"
                  label="Settings"
                  compact
                  title="Display settings"
                  @click=${() => this.settingsPopoverOpen = !this.settingsPopoverOpen}>
                  ⚙️
                </uui-button>
                <div slot="popover" class="settings-popover-content" @click=${(e: Event) => e.stopPropagation()}>
                  <div class="settings-header">Display Settings</div>
                  <div class="toggle-item">
                    <uui-toggle
                      ?checked=${this.replaceColonWithUnderscore}
                      @change=${(e: CustomEvent) => {
                        this.replaceColonWithUnderscore = (e.target as any).checked;
                        this.saveUISettings();
                      }}>
                    </uui-toggle>
                    <label @click=${() => this.replaceColonWithUnderscore = !this.replaceColonWithUnderscore}>
                      Environment variable format<br/>(<code>__</code> instead of <code>:</code>)
                    </label>
                  </div>
                  ${when(this.azureWebAppAdvancedCopy, () => html`
                    <div class="toggle-item">
                      <uui-toggle
                        ?checked=${this.showAzureColumn}
                        @change=${(e: CustomEvent) => {
                          this.showAzureColumn = (e.target as any).checked;
                          this.saveUISettings();
                        }}>
                      </uui-toggle>
                      <label @click=${() => this.showAzureColumn = !this.showAzureColumn}>
                        Enable Azure-ready JSON snippets
                      </label>
                    </div>
                  `)}
                </div>
              </uui-popover>
            </div>
            <div class="filters-section">
              <div class="toggle-item">
                <uui-toggle
                  ?checked=${this.excludeEmptyValues}
                  @change=${(e: CustomEvent) => {
                    this.excludeEmptyValues = (e.target as any).checked;
                    this.saveUISettings();
                  }}>
                </uui-toggle>
                <label @click=${() => { this.excludeEmptyValues = !this.excludeEmptyValues; this.saveUISettings(); }}>
                  Exclude empty values
                </label>
              </div>
              <div class="toggle-item">
                <uui-toggle
                  ?checked=${this.onlyRedacted}
                  ?disabled=${!this.hasAnyRedactions}
                  title=${this.hasAnyRedactions ? '' : 'No redacted values available'}
                  @change=${(e: CustomEvent) => {
                    this.onlyRedacted = (e.target as any).checked;
                    this.saveUISettings();
                  }}>
                </uui-toggle>
                <label @click=${() => { if (this.hasAnyRedactions) { this.onlyRedacted = !this.onlyRedacted; this.saveUISettings(); } }}>
                  Only redacted
                </label>
              </div>
              <div class="toggle-item">
                <uui-toggle
                  ?checked=${this.onlyStarred}
                  ?disabled=${!this.hasAnyStarred}
                  title=${this.hasAnyStarred ? '' : 'No starred settings available'}
                  @change=${(e: CustomEvent) => {
                    this.onlyStarred = (e.target as any).checked;
                    this.saveUISettings();
                  }}>
                </uui-toggle>
                <label @click=${() => { if (this.hasAnyStarred) { this.onlyStarred = !this.onlyStarred; this.saveUISettings(); } }}>
                  ⭐ Only starred
                </label>
              </div>
            </div>
            <div class="search-section">
              <uui-input
                placeholder="Type to filter..."
                .value=${this.filterText}
                @input=${(e: InputEvent) => {
                  this.filterText = (e.target as HTMLInputElement).value;
                }}>
                <uui-icon name="search" slot="prepend"></uui-icon>
              </uui-input>
            </div>
          </div>
          <div class="content-container" @scroll=${this.handleScroll}>          
            <uui-table>
              <uui-table-column style="width: 40px;"></uui-table-column>
              <uui-table-column style="width: 40%;"></uui-table-column>
              <uui-table-column style="width: 40%;"></uui-table-column>
              <uui-table-head style="background-color: #1b264f; color: white">
                <uui-table-head-cell style="width: 40px; text-align: center;">⭐</uui-table-head-cell>
                <uui-table-head-cell>Environment value path</uui-table-head-cell>
                <uui-table-head-cell>Value / Provider</uui-table-head-cell>
                ${when(this.azureWebAppAdvancedCopy && this.showAzureColumn, () => html`
                  <uui-table-head-cell style="width: 100px; text-align: center;">Azure</uui-table-head-cell>
                `)}
              </uui-table-head>
              ${when(this.visibleVariables.length, () => html`
                ${repeat(this.visibleVariables,
                  (item) => item.key,
                  (item) => html`
                    <uui-table-row>
                      <uui-table-cell class="star-cell">
                        <uui-button 
                          compact
                          look="outline"
                          label=${this.isStarred(item.key!) ? 'Unstar' : 'Star'}
                          title=${this.isStarred(item.key!) ? 'Remove from starred' : 'Add to starred'}
                          @click=${() => this.toggleStar(item.key!)}
                          class="star-button ${this.isStarred(item.key!) ? 'starred' : ''}">
                          ${this.isStarred(item.key!) ? '⭐' : '☆'}
                        </uui-button>
                      </uui-table-cell>
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
                      ${when(this.azureWebAppAdvancedCopy && this.showAzureColumn, () => html`
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
      // Load environment data and user preferences in parallel
      const [envData, prefsData] = await Promise.all([
        this.getData(),
        this.getUserPreferences()
      ]);
      
      if (envData.data) {
        this.environmentVariables = envData.data.variables || [];
        this.azureWebAppAdvancedCopy = envData.data.azureWebAppAdvancedCopy || false;
      }
      
      if (prefsData.data) {
        this.starredSettings = new Set(prefsData.data.starredSettings || []);
        
        // Load UI settings
        if (prefsData.data.uiSettings) {
          this.excludeEmptyValues = prefsData.data.uiSettings.excludeEmptyValues ?? true;
          // Only enable onlyRedacted if there are actually redacted items
          this.onlyRedacted = (prefsData.data.uiSettings.onlyRedacted ?? false) && this.hasAnyRedactions;
          // Only enable onlyStarred if there are actually starred items
          this.onlyStarred = (prefsData.data.uiSettings.onlyStarred ?? false) && this.starredSettings.size > 0;
          this.replaceColonWithUnderscore = prefsData.data.uiSettings.replaceColonWithUnderscore ?? false;
          this.showAzureColumn = prefsData.data.uiSettings.showAzureColumn ?? false;
        }
      }
    } catch (e) {
      // Optionally handle error
      console.error("Failed to load data", e);
    } finally {
      this.isLoading = false;
    }
  }

  async getData(): Promise<any> { // or a more specific type if available
    return CultivEnvironmentInspectService.getEnvironment();
  }
  
  async getUserPreferences(): Promise<any> {
    try {
      return await CultivEnvironmentInspectService.getUserPreferences();
    } catch (e) {
      console.error("Failed to load user preferences", e);
      return { data: { starredSettings: [] } };
    }
  }
  
  async saveUISettings() {
    try {
      await CultivEnvironmentInspectService.saveUiSettings({
        body: {
          excludeEmptyValues: this.excludeEmptyValues,
          onlyRedacted: this.onlyRedacted,
          onlyStarred: this.onlyStarred,
          replaceColonWithUnderscore: this.replaceColonWithUnderscore,
          showAzureColumn: this.showAzureColumn
        }
      });
    } catch (e) {
      console.error("Failed to save UI settings", e);
    }
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
        align-items: center;
        padding: 1rem;
        background-color: var(--uui-color-surface);
        border-bottom: 1px solid var(--uui-color-border);
        gap: 1rem;
        flex-wrap: wrap;
      }

      .filters-section {
        display: flex;
        gap: 1.5rem;
        flex-wrap: wrap;
        align-items: center;
      }

      .filters-section .toggle-item {
        margin-bottom: 0;
      }

      .settings-section {
        display: flex;
        align-items: center;
      }

      .search-section {
        min-width: 250px;
        flex: 1;
        max-width: 500px;
      }

      .search-section uui-input {
        width: 100%;
      }

      .search-section uui-icon {
        margin-left: 0.25rem;
      }

      @media (max-width: 1200px) {
        .search-section {
          flex-basis: 100%;
          max-width: 100%;
        }
      }

      .settings-popover-content {
        padding: 1rem;
        min-width: 320px;
        max-width: 400px;
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        background-color: var(--uui-color-surface);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        box-shadow: var(--uui-shadow-depth-3);
      }

      .settings-header {
        font-weight: bold;
        font-size: 1.1em;
        margin-bottom: 0.5rem;
        padding-bottom: 0.5rem;
        border-bottom: 1px solid var(--uui-color-border);
        color: var(--uui-color-text);
      }

      .toggle-item {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin-bottom: 0.5rem;
      }

      .toggle-item label {
        cursor: pointer;
        user-select: none;
        flex: 1;
        line-height: 1.5;
      }

      .toggle-item label:hover {
        opacity: 0.8;
      }

      .settings-popover-content .toggle-item {
        padding: 0.25rem 0;
      }

      .settings-popover-content code {
        background-color: var(--uui-color-surface-alt);
        padding: 0.1rem 0.3rem;
        border-radius: 3px;
        font-family: monospace;
        font-size: 0.9em;
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
      
      .star-cell {
        text-align: center;
        vertical-align: middle !important;
        width: 40px !important;
        max-width: 40px !important;
        padding: 0.25rem !important;
      }
      
      .star-button {
        transition: all 0.2s;
        min-width: 36px;
        color: var(--uui-color-text-alt);
      }
      
      .star-button.starred {
        color: var(--uui-color-default);
      }
      
      .star-button:hover {
        color: var(--uui-color-default);
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