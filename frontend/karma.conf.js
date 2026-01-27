// Karma configuration file for Angular 19 unit testing
// MIGRATION: New configuration file for migrated Angular 19 SPA
// See: https://karma-runner.github.io/latest/config/configuration-file.html
//
// This configuration supports:
// - Local development with Chrome browser
// - CI/CD pipelines with ChromeHeadless
// - Code coverage reporting via Istanbul
// - Jasmine test framework integration
// - Angular-specific test utilities via @angular-devkit/build-angular

module.exports = function (config) {
  config.set({
    // Base path that will be used to resolve all patterns (eg. files, exclude)
    // Empty string means the project root (where karma.conf.js is located)
    basePath: '',

    // Frameworks to use for testing
    // Available frameworks: https://www.npmjs.com/search?q=keywords:karma-adapter
    // 'jasmine' - BDD testing framework
    // '@angular-devkit/build-angular' - Angular CLI integration for TypeScript compilation
    frameworks: ['jasmine', '@angular-devkit/build-angular'],

    // List of plugins to load
    // Plugins extend Karma's functionality with launchers, reporters, and preprocessors
    plugins: [
      require('karma-jasmine'),
      require('karma-chrome-launcher'),
      require('karma-jasmine-html-reporter'),
      require('karma-coverage'),
      require('@angular-devkit/build-angular/plugins/karma')
    ],

    // Client configuration passed to the testing framework in the browser
    client: {
      // Jasmine-specific configuration options
      // See: https://jasmine.github.io/api/edge/Configuration.html
      jasmine: {
        // Disable random test execution order for deterministic test results
        // This ensures consistent test ordering across CI/CD runs
        random: false,
        // Optional: Set a specific seed for reproducible random ordering (when random: true)
        // seed: 12345,
        // Stop execution on first failure for faster feedback during development
        // failFast: false,
      },
      // Leave Jasmine Spec Runner output visible in the browser window
      // This allows developers to see test results in the browser UI
      clearContext: false
    },

    // Configuration for the Jasmine HTML reporter
    jasmineHtmlReporter: {
      // Suppress duplicate traces in the HTML output
      suppressAll: true
    },

    // Code coverage reporter configuration
    // Uses Istanbul under the hood for coverage instrumentation and reporting
    coverageReporter: {
      // Output directory for coverage reports
      // Reports will be generated in coverage/frontend/ relative to project root
      dir: require('path').join(__dirname, './coverage/frontend'),
      // Subdirectory setting - '.' means reports go directly in the dir specified
      subdir: '.',
      // Reporter types to generate
      // Multiple reporters can be specified for different use cases
      reporters: [
        // HTML reporter - generates interactive HTML coverage report
        // Useful for local development and detailed coverage analysis
        { type: 'html' },
        // LCOV reporter - generates lcov.info file for CI/CD integration
        // Compatible with coverage services like Codecov, Coveralls, SonarQube
        { type: 'lcovonly' },
        // Text summary - outputs coverage summary to console
        { type: 'text-summary' }
      ],
      // Check coverage thresholds (optional - enable for stricter quality gates)
      // check: {
      //   global: {
      //     statements: 80,
      //     branches: 80,
      //     functions: 80,
      //     lines: 80
      //   }
      // }
    },

    // List of reporters to use for test result output
    // 'progress' - displays test progress as dots/symbols in console
    // 'kjhtml' - Karma Jasmine HTML Reporter for browser-based viewing
    reporters: ['progress', 'kjhtml'],

    // Web server port for Karma
    port: 9876,

    // Enable or disable colors in the output (reporters and logs)
    colors: true,

    // Level of logging
    // Possible values: config.LOG_DISABLE, config.LOG_ERROR, config.LOG_WARN, config.LOG_INFO, config.LOG_DEBUG
    logLevel: config.LOG_INFO,

    // Enable or disable watching files and re-running tests when files change
    // When true, tests automatically re-run on file saves
    autoWatch: true,

    // Browsers to launch for testing
    // Available browser launchers: https://www.npmjs.com/search?q=keywords:karma-launcher
    // Chrome is used for local development
    // Use ChromeHeadless or ChromeHeadlessCI for CI/CD environments
    browsers: ['Chrome'],

    // Custom browser launchers configuration
    // These are used for CI/CD pipelines and headless testing environments
    customLaunchers: {
      // ChromeHeadless with CI-specific flags
      // Use this launcher in CI/CD pipelines: --browsers ChromeHeadlessCI
      ChromeHeadlessCI: {
        base: 'ChromeHeadless',
        flags: [
          // Required for running in Docker/CI environments
          '--no-sandbox',
          // Disable GPU hardware acceleration (not available in headless)
          '--disable-gpu',
          // Disable shared memory usage to avoid issues in Docker
          '--disable-dev-shm-usage',
          // Disable extensions for cleaner test environment
          '--disable-extensions',
          // Disable software rasterizer for faster execution
          '--disable-software-rasterizer'
        ]
      },
      // ChromeHeadless without additional flags (for local headless testing)
      ChromeHeadlessLocal: {
        base: 'ChromeHeadless',
        flags: [
          '--disable-gpu',
          '--disable-extensions'
        ]
      }
    },

    // Continuous Integration mode
    // If true, Karma captures browsers, runs the tests and exits
    // Set to false for development (watch mode)
    // Override via CLI: ng test --watch=false (sets singleRun to true)
    singleRun: false,

    // Restart the browser when file changes are detected
    // This ensures a fresh browser instance for each test run
    restartOnFileChange: true,

    // Timeout settings (in milliseconds)
    // Increase these if tests are timing out in CI environments
    browserDisconnectTimeout: 10000,
    browserDisconnectTolerance: 3,
    browserNoActivityTimeout: 60000,
    captureTimeout: 60000,

    // Proxies for the web server
    // Useful when tests need to access specific paths
    proxies: {},

    // Middleware configuration (if needed for custom request handling)
    // middleware: [],

    // MIME type configuration for serving files
    mime: {
      'text/x-typescript': ['ts', 'tsx']
    }
  });
};
