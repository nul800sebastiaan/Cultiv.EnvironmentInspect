import { defineConfig } from "vite";

export default defineConfig({
    build: {
        lib: {
            entry: "src/bundle.manifests.ts", // Bundle registers one or more manifests
            formats: ["es"],
        },
        outDir: "../Cultiv.EnvironmentInspect/wwwroot", // all compiled files will be placed here
        emptyOutDir: true,
        sourcemap: true,
        rollupOptions: {
            external: [/^@umbraco/], // ignore the Umbraco Backoffice package in the build
        },
    },
    base: "/App_Plugins/Cultiv.EnvironmentInspect/", // the base path of the app in the browser (used for assets)
});