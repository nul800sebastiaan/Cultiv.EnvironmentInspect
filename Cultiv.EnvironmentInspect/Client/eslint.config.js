import eslint from '@eslint/js';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  eslint.configs.recommended,
  ...tseslint.configs.recommended,
  {
    rules: {
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      '@typescript-eslint/no-explicit-any': 'warn',
    },
  },
  {
    ignores: [
      'src/api/client/**',
      'src/api/core/**',
      'src/api/types.gen.ts',
      'scripts/**',
      'node_modules/**',
      'dist/**',
      'public/**',
    ],
  }
);
