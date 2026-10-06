# Contributing

Thanks for taking the time. Bug reports, questions and pull requests are all welcome.

## Before you start

For anything larger than a small fix, open an issue first and describe what you want to change. It saves you from building something that does not fit the project's scope: a small service that turns Loki error logs into tracker issues, with no database and no third-party runtime dependencies.

## Building and testing

You need the .NET 10 SDK.

```bash
dotnet build
dotnet test
```

The build treats warnings as errors. To run the whole pipeline locally, use the demo described in the [README](README.md#try-the-demo).

## Pull requests

- Keep a pull request to one change, and say in the description what it does and why.
- Add or update tests for behaviour you change. Parsing, grouping, redaction and the tracker client are all covered by unit tests; follow the existing ones.
- Update the documentation in `README.md` and `docs/` when a setting or behaviour changes.
- Match the surrounding code: file-scoped namespaces, names that say what a thing is, constants instead of magic values.
- Do not add a runtime dependency to `src/LogTriage` without discussing it first.

## Never share real data

Do not paste real log lines, stack traces, credentials, hostnames or customer data into issues, pull requests or tests. Reduce a problem to a made-up example first. Log lines are exactly the kind of text that leaks secrets.

## Security problems

Do not report them in a public issue. See [SECURITY.md](SECURITY.md).

## License

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).
