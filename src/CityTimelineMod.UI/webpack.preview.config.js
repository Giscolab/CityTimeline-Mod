const path = require("path");
const MiniCssExtractPlugin =
  require("mini-css-extract-plugin");

const ROOT = __dirname;
const PREVIEW_ROOT =
  path.resolve(ROOT, "preview");

module.exports = {
  mode: "development",
  target: "web",

  entry: {
    preview: path.resolve(
      PREVIEW_ROOT,
      "index.tsx",
    ),
  },

  devtool: "eval-cheap-module-source-map",

  module: {
    rules: [
      {
        test: /\.tsx?$/,
        exclude: /node_modules/,
        use: {
          loader: "ts-loader",
          options: {
            configFile:
              "tsconfig.preview.json",
          },
        },
      },

      {
        test: /\.css$/,
        use: [
          MiniCssExtractPlugin.loader,
          {
            loader: "css-loader",
            options: {
              esModule: false,
              url: true,
              importLoaders: 0,
              modules: false,
            },
          },
        ],
      },

      {
        test: /\.(ttf|otf|woff2?)$/i,
        type: "asset/resource",

        generator: {
          filename:
            "fonts/[name][ext]",
        },
      },
    ],
  },

  resolve: {
    extensions: [
      ".tsx",
      ".ts",
      ".js",
    ],

    alias: {
      "cs2/api": path.resolve(
        PREVIEW_ROOT,
        "mocks/cs2-api.ts",
      ),

      "cs2/ui": path.resolve(
        PREVIEW_ROOT,
        "mocks/cs2-ui.tsx",
      ),

      "mod.json": path.resolve(
        ROOT,
        "mod.json",
      ),
    },
  },

  output: {
    path: path.resolve(
      ROOT,
      "preview-dist",
    ),

    filename: "[name].js",
    publicPath: "/",
    clean: true,
  },

  plugins: [
    new MiniCssExtractPlugin({
      filename: "[name].css",
    }),
  ],

  devServer: {
    host: "127.0.0.1",
    port: 3000,

    static: {
      directory: PREVIEW_ROOT,
      watch: true,
    },

    devMiddleware: {
      publicPath: "/",
    },

    hot: false,
    liveReload: true,
    open: true,

    client: {
      overlay: {
        errors: true,
        warnings: false,
      },
    },
  },
};