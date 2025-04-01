import { manifests as entrypoints } from './entrypoints/manifest';
import { manifests as dashboards } from './dashboards/manifest';
//import { ENTITY_ALIAS, PACKAGE_NAME } from './constants.ts';

export const manifests: Array<UmbExtensionManifest> = [
    ...entrypoints,
    ...dashboards,
];