module.exports = {
  corePlugins: {
    preflight: false,
  },
  content: [
    '../Core/**/*.{razor,html,cshtml}',
    '../Modules/**/*.{razor,html,cshtml}',
    '../MAP.H.Desktop/**/*.{razor,html,cshtml}',
    '../MAP.H.Web/**/*.{razor,html,cshtml}',
  ],
}
