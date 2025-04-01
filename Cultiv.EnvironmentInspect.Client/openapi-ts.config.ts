import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
    input:
        'https://localhost:44347/umbraco/swagger/environmentinspect-api/swagger.json',
    output: {
        format: 'prettier',
        lint: 'eslint',
        path: './src/api',
    },
    plugins: [
        {
            name: '@hey-api/client-fetch'
        },
        '@hey-api/schemas',
        {
            dates: true,
            name: '@hey-api/transformers',
        },
        {
            enums: 'javascript',
            name: '@hey-api/typescript',
        },
        {
            name: '@hey-api/sdk',
            transformer: true,
        },
    ],
});
