import { LitElement, css, html, customElement, property, repeat, when, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { EnvironmentVariable, EnvironmentInspectResponse, UserPreferencesDto } from "../api/types.gen";
import { CultivEnvironmentInspectService } from "../api/sdk.gen";

// Type for elements with checked property (like uui-toggle)
interface CheckableElement extends HTMLElement {
  checked: boolean;
}

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
  @state() selectedForAzure: Set<string> = new Set(); // Track selected items for bulk Azure copy
  @state() hasRedactions: boolean = false; // Set from server
  @state() isLocal: boolean = false; // Set from server
  @state() isUmbracoCloud: boolean = false; // Set from server
  @state() dismissInfoPanel: boolean = false; // User preference to dismiss info panel
  @state() defaultConfigTemplate: string = ''; // Server-side default template
  @state() umbracoCloudConfigTemplate: string = ''; // Server-side Umbraco Cloud template
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

  generateBulkAzureWebAppSnippet(): string {
    const selectedItems = this.filteredVariables.filter(v => this.selectedForAzure.has(v.key!));
    const snippets = selectedItems.map(item => ({
      name: this.formatKey(item.key!),
      value: item.value || '',
      slotSetting: false
    }));
    return JSON.stringify(snippets, null, 2);
  }

  toggleAzureSelection(key: string) {
    if (this.selectedForAzure.has(key)) {
      this.selectedForAzure.delete(key);
    } else {
      this.selectedForAzure.add(key);
    }
    this.requestUpdate();
  }

  toggleAllAzureSelection() {
    const allSelected = this.filteredVariables.every(v => this.selectedForAzure.has(v.key!));
    if (allSelected) {
      // Deselect all filtered items
      this.filteredVariables.forEach(v => this.selectedForAzure.delete(v.key!));
    } else {
      // Select all filtered items
      this.filteredVariables.forEach(v => this.selectedForAzure.add(v.key!));
    }
    this.requestUpdate();
  }

  async copyBulkAzureSnippet() {
    if (this.selectedForAzure.size === 0) return;
    
    const snippet = this.generateBulkAzureWebAppSnippet();
    await this.copyToClipboard(snippet);
    
    // Clear selection after copy
    this.selectedForAzure.clear();
    this.requestUpdate();
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

  renderInfoPanel() {
    // If redactions are already configured, show simplified help panel
    if (this.hasRedactions) {
      return html`
        <uui-box class="info-panel info-panel-success" headline="✅ Redactions are Configured">
          <uui-button
            slot="header-actions"
            compact
            look="secondary"
            label="Dismiss"
            title="Hide this panel"
            @click=${this.dismissInfo}>
            <uui-icon name="icon-delete"></uui-icon>
          </uui-button>
          <div class="info-content">
            <p>Your configuration includes redaction rules to protect sensitive values.</p>
            <p><strong>Helpful resources:</strong></p>
            <ul>
              <li><a href="https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/blob/develop/v2/CONFIGURATION.md" target="_blank">Configuration Guide</a> - Learn about all available options</li>
              <li><a href="https://github.com/nul800sebastiaan/Cultiv.EnvironmentInspect/blob/develop/v2/README.md" target="_blank">Documentation</a> - Package features and usage</li>
            </ul>
          </div>
        </uui-box>
      `;
    }

    // Show full onboarding for first-time users (no redactions)
    const configSnippet = this.isUmbracoCloud ? this.umbracoCloudConfigTemplate : this.defaultConfigTemplate;
    const isOnline = !this.isLocal;

    let title = '';
    let message = '';
    let showApplyButton = false;

    if (this.isLocal && this.isUmbracoCloud) {
      title = '🔧 Configure Redactions for Umbraco Cloud';
      message = 'Add default Umbraco Cloud redaction rules to protect sensitive configuration values.';
      showApplyButton = true;
    } else if (this.isLocal && !this.isUmbracoCloud) {
      title = '🔧 Configure Redactions';
      message = 'Add default redaction rules to protect sensitive configuration values like passwords and connection strings.';
      showApplyButton = true;
    } else if (isOnline && this.isUmbracoCloud) {
      title = '📋 Umbraco Cloud Configuration';
      message = 'Add these redaction rules to your appsettings.json file to protect sensitive Umbraco Cloud values. Copy the configuration below and commit it to your repository.';
      showApplyButton = false;
    } else {
      title = '📋 Recommended Configuration';
      message = 'Add these redaction rules to your appsettings.json file to protect sensitive values. Copy the configuration below and commit it to your repository.';
      showApplyButton = false;
    }

    return html`
      <uui-box class="info-panel" headline=${title}>
        <uui-button
          slot="header-actions"
          compact
          look="secondary"
          label="Dismiss"
          title="Hide this panel"
          @click=${this.dismissInfo}>
          <uui-icon name="icon-delete"></uui-icon>
        </uui-button>
        <div class="info-content">
          <p>${message}</p>
          ${when(showApplyButton, () => html`
            <p><strong>Note:</strong> Configuration changes take effect immediately thanks to hot reload.</p>
          `, () => html`
            <p><strong>Note:</strong> Configuration changes require a restart or redeployment to take effect in production.</p>
          `)}
          <div class="config-snippet-container">
            <div class="config-snippet-header">
              <span class="config-label">${this.isUmbracoCloud ? 'Umbraco Cloud Configuration' : 'Default Configuration'}</span>
              <div class="config-actions">
                ${when(showApplyButton, () => html`
                  <uui-button
                    compact
                    look="primary"
                    color="positive"
                    label="Apply now"
                    title="Apply configuration to appsettings.json"
                    @click=${() => this.applyConfiguration()}>
                    ✅ Apply now
                  </uui-button>
                `)}
                <uui-button
                  compact
                  look=${showApplyButton ? 'secondary' : 'primary'}
                  label="Copy configuration"
                  title="Copy to clipboard"
                  @click=${() => this.copyConfigSnippet(configSnippet)}>
                  📋 Copy
                </uui-button>
              </div>
            </div>
            <pre class="config-snippet"><code>${configSnippet}</code></pre>
          </div>
          <div class="info-links">
            <a href="https://github.com/cultiv-environmentinspect/Cultiv.EnvironmentInspect#readme" target="_blank" rel="noopener noreferrer">
              📖 Documentation
            </a>
            <span class="link-separator">•</span>
            <a href="https://github.com/cultiv-environmentinspect/Cultiv.EnvironmentInspect/blob/main/CONFIGURATION.md" target="_blank" rel="noopener noreferrer">
              ⚙️ Configuration Guide
            </a>
          </div>
        </div>
      </uui-box>
    `;
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
                        this.replaceColonWithUnderscore = (e.target as CheckableElement).checked;
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
                          this.showAzureColumn = (e.target as CheckableElement).checked;
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
                    this.excludeEmptyValues = (e.target as CheckableElement).checked;
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
                    this.onlyRedacted = (e.target as CheckableElement).checked;
                    this.saveUISettings();
                  }}>
                </uui-toggle>
                <label @click=${() => { if (this.hasAnyRedactions) { this.onlyRedacted = !this.onlyRedacted; this.saveUISettings(); } }}>
                  🔒 Redacted
                </label>
              </div>
              <div class="toggle-item">
                <uui-toggle
                  ?checked=${this.onlyStarred}
                  ?disabled=${!this.hasAnyStarred}
                  title=${this.hasAnyStarred ? '' : 'No starred settings available'}
                  @change=${(e: CustomEvent) => {
                    this.onlyStarred = (e.target as CheckableElement).checked;
                    this.saveUISettings();
                  }}>
                </uui-toggle>
                <label @click=${() => { if (this.hasAnyStarred) { this.onlyStarred = !this.onlyStarred; this.saveUISettings(); } }}>
                  ⭐ Starred
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
                ${when(this.filterText, () => html`
                  <uui-button 
                    slot="append"
                    compact
                    look="secondary"
                    label="Clear filter"
                    @click=${() => this.filterText = ''}>
                    <uui-icon name="wrong"></uui-icon>
                  </uui-button>
                `)}
              </uui-input>
              ${when(!this.shouldShowInfoPanel, () => html`
                <div class="help-button-wrapper" style="margin-left: 0.5rem;">
                  <uui-button
                    compact
                    look="outline"
                    label="Show help"
                    title="${!this.hasRedactions ? 'No redactions configured - click for setup help' : 'Show configuration help panel'}"
                    @click=${this.showInfo}>
                    <uui-icon name="icon-help-alt"></uui-icon>
                  </uui-button>
                  ${when(!this.hasRedactions, () => html`
                    <uui-icon class="warning-indicator" name="icon-alert"></uui-icon>
                  `)}
                </div>
              `)}
            </div>
          </div>
          ${when(this.shouldShowInfoPanel, () => this.renderInfoPanel())}
          ${when(this.azureWebAppAdvancedCopy && this.showAzureColumn && this.selectedForAzure.size > 0, () => html`
            <div class="bulk-action-bar">
              <span class="selection-count">${this.selectedForAzure.size} item${this.selectedForAzure.size === 1 ? '' : 's'} selected</span>
              <uui-button
                look="primary"
                label="Copy selected as Azure JSON"
                @click=${this.copyBulkAzureSnippet}>
                ☁️ Copy ${this.selectedForAzure.size} item${this.selectedForAzure.size === 1 ? '' : 's'}
              </uui-button>
              <uui-button
                look="secondary"
                label="Clear selection"
                @click=${() => { this.selectedForAzure.clear(); this.requestUpdate(); }}>
                Clear
              </uui-button>
            </div>
          `)}
          <div class="content-container" @scroll=${this.handleScroll}>          
            <uui-table>
              <uui-table-column style="width: 40px;"></uui-table-column>
              <uui-table-column style="width: 40%;"></uui-table-column>
              <uui-table-column style="width: 40%;"></uui-table-column>
              ${when(this.azureWebAppAdvancedCopy && this.showAzureColumn, () => html`
                <uui-table-column style="width: 100px;"></uui-table-column>
                <uui-table-column style="width: 40px;"></uui-table-column>
              `)}
              <uui-table-head style="background-color: #1b264f; color: white">
                <uui-table-head-cell style="width: 40px; text-align: center;">⭐</uui-table-head-cell>
                <uui-table-head-cell>Environment value path</uui-table-head-cell>
                <uui-table-head-cell>Value / Provider</uui-table-head-cell>
                ${when(this.azureWebAppAdvancedCopy && this.showAzureColumn, () => html`
                  <uui-table-head-cell style="width: 100px; text-align: center;">Azure</uui-table-head-cell>
                  <uui-table-head-cell style="width: 40px; text-align: center;">
                    <input 
                      type="checkbox"
                      class="select-all-checkbox"
                      .checked=${this.filteredVariables.length > 0 && this.filteredVariables.every(v => this.selectedForAzure.has(v.key!))}
                      @change=${() => this.toggleAllAzureSelection()}
                      title="Select/deselect all items (${this.filteredVariables.length} total)"
                    />
                  </uui-table-head-cell>
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
                        <uui-table-cell class="checkbox-cell">
                          <input 
                            type="checkbox"
                            class="azure-select-checkbox"
                            .checked=${this.selectedForAzure.has(item.key!)}
                            @change=${() => this.toggleAzureSelection(item.key!)}
                            title="Select for bulk Azure copy"
                          />
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
      // Load environment data, user preferences, and templates in parallel
      const [envData, prefsData, templatesData] = await Promise.all([
        this.getData(),
        this.getUserPreferences(),
        this.getConfigurationTemplates()
      ]);
      
      if (envData.data) {
        this.environmentVariables = envData.data.variables || [];
        this.azureWebAppAdvancedCopy = envData.data.azureWebAppAdvancedCopy || false;
        this.hasRedactions = envData.data.hasRedactions || false;
        this.isLocal = envData.data.isLocal || false;
        this.isUmbracoCloud = envData.data.isUmbracoCloud || false;
        
        // Set initial dismissInfoPanel state based on whether redactions exist
        // If redactions are configured, hide panel by default (user already configured)
        // If no redactions, show panel by default (help user get started)
        // This will be overridden by saved preference below if one exists
        this.dismissInfoPanel = this.hasRedactions;
      }
      
      if (templatesData.data) {
        this.defaultConfigTemplate = templatesData.data.defaultTemplate || '';
        this.umbracoCloudConfigTemplate = templatesData.data.umbracoCloudTemplate || '';
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
          this.dismissInfoPanel = prefsData.data.uiSettings.dismissInfoPanel ?? false;
        }
      }
    } catch (e) {
      // Optionally handle error
      console.error("Failed to load data", e);
    } finally {
      this.isLoading = false;
    }
  }

  async getData(): Promise<{ data?: EnvironmentInspectResponse }> {
    return CultivEnvironmentInspectService.getEnvironment();
  }
  
  async getUserPreferences(): Promise<{ data?: UserPreferencesDto }> {
    try {
      return await CultivEnvironmentInspectService.getUserPreferences();
    } catch (e) {
      console.error("Failed to load user preferences", e);
      return { data: { starredSettings: [], uiSettings: { excludeEmptyValues: true, onlyRedacted: false, onlyStarred: false, replaceColonWithUnderscore: false, showAzureColumn: false, dismissInfoPanel: false } } };
    }
  }
  
  async getConfigurationTemplates(): Promise<{ data?: any }> {
    try {
      return await CultivEnvironmentInspectService.getConfigurationTemplates();
    } catch (e) {
      console.error("Failed to load configuration templates", e);
      return { data: { defaultTemplate: '', umbracoCloudTemplate: '' } };
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
          showAzureColumn: this.showAzureColumn,
          dismissInfoPanel: this.dismissInfoPanel
        }
      });
    } catch (e) {
      console.error("Failed to save UI settings", e);
    }
  }

  dismissInfo = async () => {
    this.dismissInfoPanel = true;
    await this.saveUISettings();
  }

  showInfo = async () => {
    this.dismissInfoPanel = false;
    await this.saveUISettings();
  }

  get shouldShowInfoPanel(): boolean {
    // Show panel based on user preference (help button shows, dismiss hides)
    return !this.dismissInfoPanel;
  }

  async copyConfigSnippet(config: string) {
    await this.copyToClipboard(config);
  }

  async applyConfiguration() {
    const configSnippet = this.isUmbracoCloud ? this.umbracoCloudConfigTemplate : this.defaultConfigTemplate;
    
    // Show Umbraco confirmation modal
    try {
      await umbConfirmModal(this, {
        headline: 'Apply Configuration',
        content: html`
          <p>This will update your <strong>appsettings.json</strong> file with the default redaction rules.</p>
          <p>Changes will take effect immediately thanks to hot reload.</p>
          <p>Do you want to continue?</p>
        `,
        color: 'positive',
        confirmLabel: 'Apply Configuration',
        cancelLabel: 'Cancel'
      });
    } catch {
      // User cancelled
      return;
    }
    
    try {
      const { error } = await CultivEnvironmentInspectService.applyConfiguration({
        body: { configJson: configSnippet }
      });
      
      if (error) {
        console.error('API error response:', error);
        throw new Error(`Failed to apply configuration: ${JSON.stringify(error)}`);
      }
      
      const notificationContext = await this.getContext(UMB_NOTIFICATION_CONTEXT);
      notificationContext?.peek('positive', { 
        data: { 
          headline: 'Configuration Applied',
          message: 'Configuration has been written to appsettings.json and is now active.' 
        } 
      });
      
      // Dismiss the info panel after successful apply
      await this.dismissInfo();
      
      // Give hot reload a moment to process the configuration change
      // before fetching the updated data
      await new Promise(resolve => setTimeout(resolve, 500));
      
      // Reload environment data to reflect the new redactions
      const envData = await this.getData();
      if (envData.data) {
        this.environmentVariables = envData.data.variables || [];
        this.hasRedactions = envData.data.hasRedactions || false;
        this.azureWebAppAdvancedCopy = envData.data.azureWebAppAdvancedCopy || false;
      }
      
    } catch (err) {
      console.error('Failed to apply configuration: ', err);
      
      const notificationContext = await this.getContext(UMB_NOTIFICATION_CONTEXT);
      notificationContext?.peek('danger', { 
        data: { 
          headline: 'Configuration Failed',
          message: 'Could not apply configuration to appsettings.json. Check logs for details.' 
        } 
      });
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
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }

      .search-section uui-input {
        flex: 1;
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

      .info-panel {
        margin: 1rem;
        background-color: var(--uui-color-surface);
        border: 2px solid var(--uui-color-warning);
        border-radius: var(--uui-border-radius);
        box-shadow: var(--uui-shadow-depth-2);
      }

      .info-panel-success {
        border-color: var(--uui-color-positive);
      }

      .info-content {
        padding: 0;
      }

      .info-content p {
        margin: 0 0 1rem 0;
        line-height: 1.6;
        color: var(--uui-color-text);
      }

      .info-content strong {
        font-weight: 600;
      }

      .config-snippet-container {
        margin: 1rem 0;
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        overflow: hidden;
      }

      .config-snippet-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 0.75rem 1rem;
        background-color: var(--uui-color-surface-alt);
        border-bottom: 1px solid var(--uui-color-border);
      }

      .config-actions {
        display: flex;
        gap: 0.5rem;
      }

      .config-label {
        font-weight: 600;
        font-size: 0.95em;
        color: var(--uui-color-text);
      }

      .config-snippet {
        margin: 0;
        padding: 1rem;
        background-color: var(--uui-color-surface-alt);
        overflow-x: auto;
        max-height: 400px;
        overflow-y: auto;
      }

      .config-snippet code {
        font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
        font-size: 0.85em;
        line-height: 1.5;
        color: var(--uui-color-text);
        white-space: pre;
      }

      .info-links {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding-top: 0.5rem;
        border-top: 1px solid var(--uui-color-border);
        margin-top: 1rem;
      }

      .info-links a {
        color: var(--uui-color-interactive);
        text-decoration: none;
        font-weight: 500;
        transition: color 0.2s;
      }

      .help-button-wrapper {
        position: relative;
        display: inline-block;
      }

      .help-button-wrapper .warning-indicator {
        position: absolute;
        top: -8px;
        right: -8px;
        pointer-events: none;
        color: var(--uui-color-warning);
        font-size: 16px;
        line-height: 1;
        background-color: white;
        border-radius: 50%;
        width: 20px;
        height: 20px;
        display: flex;
        align-items: center;
        justify-content: center;
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
      }

      .info-links a:hover {
        color: var(--uui-color-interactive-emphasis);
        text-decoration: underline;
      }

      .link-separator {
        color: var(--uui-color-border);
      }

      .content-container {
        flex: 1;
        overflow-y: auto;
        min-height: 0;
      }
      
      :host {
        display: flex;
        flex-direction: column;
        height: 100%;
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
      
      .checkbox-cell {
        text-align: center;
        vertical-align: middle !important;
        width: 40px !important;
        max-width: 40px !important;
        padding: 0.25rem !important;
      }
      
      .azure-select-checkbox,
      .select-all-checkbox {
        cursor: pointer;
        width: 18px;
        height: 18px;
      }
      
      .bulk-action-bar {
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 0.75rem 1rem;
        background-color: var(--uui-color-selected);
        border-bottom: 1px solid var(--uui-color-border);
        box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
      }
      
      .bulk-action-bar .selection-count {
        font-weight: 500;
        color: white;
        margin-right: auto;
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