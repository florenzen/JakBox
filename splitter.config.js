var path = require('path')

function resolve(filePath) {
  return path.resolve(__dirname, filePath)
}

var define = [];
var idx = process.argv.indexOf("--define");

if (idx > -1) {
  define = [process.argv[idx + 1]];
}

console.log("Bundling for " + define + "...");

module.exports = {
  entry: resolve('src/JakBox.fsproj'),
  outDir: resolve("out"),
  babel: {
    filename: "JakBox.js",
    presets: ["module:metro-react-native-babel-preset"],
    sourceMaps: true,
    minified: false,    
    compact: false
  },
  fable: {
    define: define
  }
};