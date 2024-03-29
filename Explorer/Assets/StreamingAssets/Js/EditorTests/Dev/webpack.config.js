const path = require('path');

module.exports = {
    mode: 'production',
    output: {
        path: path.resolve(__dirname, 'dist'),
        filename: 'joi-validates.min.js',
        library: 'Validates',
        libraryTarget: 'umd',
        umdNamedDefine: true
    },
    module: {
        rules: [
            {
                test: /\.js$/,
                use: 'ts-loader',
                exclude: /node_modules/,
            }
        ]
    },
    resolve: {
        extensions: ['.ts', '.js'],
    },
    externals: {
        lodash: {
            commonjs: 'lodash',
            commonjs2: 'lodash',
            amd: 'lodash',
            root: '_'
        }
    }
};