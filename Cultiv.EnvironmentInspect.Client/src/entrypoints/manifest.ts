export const manifests: Array<UmbExtensionManifest> = [
  {
    name: "Cultiv EnvironmentInspect Entrypoint",
    alias: "Cultiv.EnvironmentInspect.Entrypoint",
    type: "backofficeEntryPoint",
    js: () => import("./entrypoint"),
  }
];
