---
description: "Use when working on the frontend dashboard in Client/. Covers Lit web component patterns, Umbraco backoffice conventions, Vite build setup, ESLint rules, and TypeScript API client usage."
applyTo: "Cultiv.EnvironmentInspect/Client/**"
---

# Frontend Development Guidelines

The frontend lives in `Cultiv.EnvironmentInspect/Client/` and is built with **TypeScript**, **Lit** (web components), and **Vite**.

## Key Commands

```bash
cd Cultiv.EnvironmentInspect/Client

npm run build          # Production build → ../wwwroot/App_Plugins/
npm run watch          # Watch mode (auto-rebuild on save)
npm run lint           # Run ESLint
npm run generate-client  # Regenerate TypeScript API client (requires demo site running)
```

## TypeScript API Client

The typed API client lives in `src/api/` and is **auto-generated** — do not edit these files manually.

To regenerate after backend changes:
1. Start the demo site: `cd Cultiv.EnvironmentInspect.DemoSite && dotnet run`
2. In a new terminal: `cd Cultiv.EnvironmentInspect/Client && npm run generate-client`

Import and use the generated services:
```typescript
import { ConfigurationService } from '../api/index.js';

const result = await ConfigurationService.getConfiguration();
```

## Lit Web Components

All UI components are Lit `LitElement` subclasses. Follow the Umbraco backoffice patterns:

```typescript
import { LitElement, html, css, customElement, state } from '@umbraco-cms/backoffice/external/lit';

@customElement('cultiv-my-component')
export class MyComponent extends LitElement {
    @state() private _data: SomeType[] = [];

    static override styles = css`
        :host { display: block; }
    `;

    override render() {
        return html`<div>${this._data.map(item => html`<span>${item.name}</span>`)}</div>`;
    }
}
```

- Use `@state()` for private reactive state, `@property()` for public attributes
- Prefer `override` keyword on `render()`, `connectedCallback()`, `disconnectedCallback()`
- Use Umbraco's built-in UI components from `@umbraco-cms/backoffice` where available

## ESLint

Config is in `eslint.config.js`. Run `npm run lint` before committing.
TypeScript strict mode is enabled — avoid `any` types.

## Build Output

Vite outputs to `../wwwroot/App_Plugins/CultivEnvironmentInspect/`. The `umbraco-package.json` manifest references these built assets — do not rename output files without also updating the manifest.

## Debugging

- Open browser DevTools in the Umbraco backoffice
- Source maps are included in development/watch builds
- Backend API errors appear in the Network tab — check for HTTP 4xx/5xx responses
