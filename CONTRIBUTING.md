# Contributing to Cocoar.FileSystem

Thank you for your interest in contributing! We welcome contributions of all kinds.

## How to Contribute

1. **Fork the repository** and create your branch from `develop`
2. **Make your changes** with clear, descriptive commits
3. **Add tests** if you're adding functionality
4. **Ensure tests pass** on all platforms
5. **Submit a pull request** to the `develop` branch

## Development Setup

### Prerequisites
- .NET 8.0 SDK or later
- Git

### Building
```bash
cd src
dotnet restore
dotnet build
```

> **Note**: This project uses the new `.slnx` solution file format (XML-based). Visual Studio 2022 17.8+ and JetBrains Rider 2023.3+ support this format natively.

### Running Tests
```bash
cd src
dotnet test
```

## Coding Guidelines

- Follow existing code style
- Write XML documentation for public APIs
- Keep methods focused and concise
- Add tests for new functionality
- Ensure cross-platform compatibility

## Pull Request Process

1. Update the CHANGELOG.md with details of your changes
2. Ensure all tests pass on Windows, Linux, and macOS
3. Update documentation if you're changing functionality
4. Your PR will be reviewed by maintainers

## Versioning

We use [Semantic Versioning](https://semver.org/):
- MAJOR: Breaking changes
- MINOR: New features (backward compatible)
- PATCH: Bug fixes (backward compatible)

## Questions?

Feel free to open an issue for any questions or concerns.

## License

By contributing, you agree that your contributions will be licensed under the Apache License 2.0.
