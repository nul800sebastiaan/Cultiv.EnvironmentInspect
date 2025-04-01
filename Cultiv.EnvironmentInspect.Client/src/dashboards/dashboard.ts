import { LitElement, css, html, customElement, property, repeat, when } from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";

import { getUmbracoManagementApiV1EnvironmentinspectGetenvironment } from '../api/sdk.gen';
import { DebugViewModel } from "../api/types.gen";
import { RequestResult } from "@hey-api/client-fetch";

@customElement('environmentinspect-dashboard')
export class EnvironmentInspectDashboardElement extends UmbElementMixin(LitElement) {

    @property({ type: Object }) environmentVariables: Array<(DebugViewModel)>;

  render() {
    return html`
        <uui-table>
          <uui-table-column style="width: 50%;"></uui-table-column>
          <uui-table-head  style="background-color: #1b264f; color: white">
            <uui-table-head-cell>Environment value path	</uui-table-head-cell>
            <uui-table-head-cell>Value / Provider</uui-table-head-cell>            
          </uui-table-head>
            ${when(this.environmentVariables, () => html`
              ${repeat(this.environmentVariables!,
                  (item) => item.key,
                  (item) => html`
                      <uui-table-row>
                        <uui-table-cell clip-text="">${item.key}</uui-table-cell>
                        <uui-table-cell clip-text=""><em style="font-size: 0.8em">${item.provider}</em>
                        <br/>
                        ${item.value}
                        </uui-table-cell>
                        
                      </uui-table-row>
              `)}
            `)}
      </uui-table>
    `;
  }

    async firstUpdated() {
        const { data } = await this.getData(); 

    if(data) {
        this.environmentVariables = data;
    }
      
  }

  async getData(): Promise<RequestResult<Array<(DebugViewModel)>>>
  {
        return getUmbracoManagementApiV1EnvironmentinspectGetenvironment();
  }

  static styles = [
    css`
      h4 {
        font-size: var(--uui-type-h4-size);
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