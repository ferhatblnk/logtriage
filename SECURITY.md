# Security

## Reporting a vulnerability

Please do not open a public issue for a security problem. Use **Report a vulnerability** under the repository's *Security* tab (GitHub private vulnerability reporting), and include what you found, how to reproduce it and the version or commit you tested.

This is a volunteer-maintained project. Reports are handled on a best-effort basis; there is no guaranteed response time. Fixes are released for the latest version only.

## What LogTriage handles

LogTriage reads log lines, which often contain personal or confidential data, and copies parts of them into an issue tracker that more people can read. Keep that in mind when you deploy it:

- **Credentials.** The Loki and Jira credentials are read from configuration and are never logged. Supply them as environment variables or through your secret store. Do not commit them.
- **What is forwarded.** Exception types, messages and stack traces are forwarded as they were logged. Request data is forwarded only when `Issue:IncludeRequestData` is on, after redaction.
- **Redaction is best effort.** It masks fields by name and withholds requests that look like they contain payment details. It cannot recognise every secret. If your logs can contain data that must not reach your tracker, leave request data off and review what your services put into exception messages.
- **The API.** It is disabled until `Api:Key` is set, is read-only, and exposes exception messages. It has no TLS and no user accounts; keep it on an internal network or behind your own gateway.
- **The Jira account.** Use a dedicated account with the minimum permissions: browse the project, create issues, and assign issues if needed.

## The demo

`samples/` and `docker-compose.yml` exist to try the pipeline. The sample API logs request bodies, including a made-up password and a public test card number, to show the redaction. The Jira stand-in accepts any credentials. Neither is meant to be deployed.
