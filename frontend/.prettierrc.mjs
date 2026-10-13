/**
 * @see https://prettier.io/docs/en/configuration.html
 * @type {import("prettier").Config}
 */
const config = {
    plugins: ['prettier-plugin-organize-attributes', 'prettier-plugin-tailwindcss'],
    singleQuote: true,
    printWidth: 120,
    bracketSameLine: true,
    htmlWhitespaceSensitivity: 'ignore',
    experimentalOperatorPosition: 'start',

    // prettier-plugin-organize-attributes
    attributeGroups: [
        '$ANGULAR_ELEMENT_REF',
        '$ANGULAR_STRUCTURAL_DIRECTIVE',
        '$ID',
        '$CLASS',
        '$DEFAULT',
        '$ANGULAR_ANIMATION',
        '$ANGULAR_ANIMATION_INPUT',
        '$ANGULAR_TWO_WAY_BINDING',
        '$ANGULAR_INPUT',
        '$ANGULAR_OUTPUT',
    ],

    overrides: [
        {
            files: ['./src/assets/i18n/**'],
            options: {
                plugins: ['prettier-plugin-sort-json'],

                // prettier-plugin-sort-json
                jsonRecursiveSort: true,
            },
        },
    ],
};

export default config;
