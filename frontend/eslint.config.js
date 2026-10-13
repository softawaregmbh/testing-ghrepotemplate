// @ts-check
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

module.exports = tseslint.config(
    {
        ignores: ['.angular/', '.nx/'],
    },
    {
        files: ['**/*.ts'],
        extends: [
            eslint.configs.recommended,
            ...tseslint.configs.strict,
            ...tseslint.configs.stylistic,
            ...angular.configs.tsRecommended,
        ],
        processor: angular.processInlineTemplates,
        rules: {
            quotes: [
                'error',
                'single',
                {
                    avoidEscape: true,
                },
            ],
            semi: ['error', 'always'],

            // NOTE: For "tslint:disable" used in the generated api manager
            '@typescript-eslint/ban-tslint-comment': 'off',

            '@angular-eslint/directive-selector': [
                'error',
                {
                    type: 'attribute',
                    prefix: 'app',
                    style: 'camelCase',
                },
            ],
            '@angular-eslint/component-selector': [
                'error',
                {
                    type: 'element',
                    prefix: 'app',
                    style: 'kebab-case',
                },
            ],

            '@typescript-eslint/explicit-member-accessibility': [
                'error',
                {
                    accessibility: 'explicit',
                    overrides: {
                        constructors: 'off',
                    },
                },
            ],

            '@typescript-eslint/no-extraneous-class': ['error', { allowWithDecorator: true }],
        },
    },
    {
        files: ['**/*.html'],
        extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
        rules: {},
    },
);
