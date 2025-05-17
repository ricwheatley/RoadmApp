/* eslint.config.js – flat config, ready for React + Jest */
import js from "@eslint/js";
import reactPlugin from "eslint-plugin-react";
import jestPlugin from "eslint-plugin-jest";

export default [
  /* Core JavaScript rules */
  js.configs.recommended,

  /* React / JSX files */
  {
    files: ["**/*.{js,jsx}"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      parserOptions: { ecmaFeatures: { jsx: true } },
    },
    plugins: { react: reactPlugin },
    settings: { react: { version: "detect" } },
    rules: {
      // tweak to taste
      "react/react-in-jsx-scope": "off", // not needed since React 17
    },
  },

  /* Jest test files */
  {
    /* any folder called __tests__, *.test.js(x) or *.spec.js(x) */
    files: ["**/__tests__/**/*", "**/*.{test,spec}.{js,jsx}"],

    /* pull in the plugin’s ready-made flat config */
    ...jestPlugin.configs["flat/recommended"],

    /* ESLint still needs to see the globals in the new “readonly/writable” format */
    languageOptions: {
      globals: jestPlugin.environments.globals.globals,
    },
    rules: {
    'no-unused-vars': 'off',    
  },
  },
];
