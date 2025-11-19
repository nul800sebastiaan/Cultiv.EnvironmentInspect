export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Cultiv Environment Inspect Entrypoint",
    alias: "Cultiv.EnvironmentInspect.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint.js"),
  },
];
