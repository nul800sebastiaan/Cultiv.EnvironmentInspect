export const manifests: Array<UmbExtensionManifest> = [
    {
        type: 'dashboard',
        alias: 'cultiv.environmentinspect.dashboard',
        name: 'Cultiv.EnvironmentInspect',
        element: () => import("./dashboard"),
        elementName: "environmentinspect-dashboard",

        weight: 200,
        meta: {
            "label": "🔎 Environment",
            "pathname": "environmentinspect-dashboard"
        },
        "conditions": [
            {
                "alias": "Umb.Condition.SectionAlias",
                "match": "Umb.Section.Settings"
            }
        ]
    }
];
