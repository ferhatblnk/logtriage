# Configuration

Every setting can be given as an environment variable or in `appsettings.json`. Environment variables use two underscores between section and name: `Sync:DryRun` becomes `Sync__DryRun`.

Invalid settings stop the service at start-up with a message that names the setting.

## Loki

| Setting | Default | Description |
| --- | --- | --- |
| `Loki:Url` | `http://localhost:3100` | Base URL of Loki. |
| `Loki:Query` | ``{service_name=~`.+`} \|= `exception.type` `` | LogQL log query that returns the error lines. It may return other lines too; lines without an exception type are skipped. Metric queries are rejected. |
| `Loki:ServiceLabel` | `service_name` | Stream label that holds the service name. |
| `Loki:EnvironmentLabel` | `env` | Stream label that holds the environment. Optional in your streams. |
| `Loki:TenantId` | empty | Sent as `X-Scope-OrgID`. Needed when Loki runs in multi-tenant mode. |
| `Loki:Username`, `Loki:Password` | empty | Basic authentication, for example for a hosted Loki. |
| `Loki:PageSize` | `1000` | Lines requested per query, 1 to 5000. Must not exceed Loki's `max_entries_limit_per_query`. |
| `Loki:MaxEvents` | `20000` | Lines read per pass before the result is reported as truncated. |
| `Loki:TimeoutSeconds` | `30` | Timeout of a single query. |

## Fields

The names of the fields read from each log line. See [log-contract.md](log-contract.md) for how they are looked up.

| Setting | Default |
| --- | --- |
| `Fields:Root` | empty |
| `Fields:ExceptionType` | `exception.type` |
| `Fields:Message` | `exception.message` |
| `Fields:StackTrace` | `exception.stacktrace` |
| `Fields:Method` | `http.request.method` |
| `Fields:Route` | `http.route` |
| `Fields:Path` | `url.path` |
| `Fields:Query` | `url.query` |
| `Fields:Body` | `http.request.body` |
| `Fields:StatusCode` | `http.response.status_code` |

## Sync

| Setting | Default | Description |
| --- | --- | --- |
| `Sync:DryRun` | `true` | Log what would be opened and leave Jira alone. Set to `false` to create issues. |
| `Sync:IntervalSeconds` | `300` | Time between runs. The first run starts immediately. Minimum 5. |
| `Sync:LookbackMinutes` | `60` | Window each run reads. Occurrence counts in an issue refer to it. |
| `Sync:MinStatusCode` | `500` | Groups with a lower HTTP status are ignored. Groups without a status always qualify. |
| `Sync:MinOccurrences` | `1` | Occurrences required inside the window. |
| `Sync:MaxIssuesPerRun` | `5` | Issues opened per run, most frequent groups first. |
| `Sync:NotBefore` | empty | ISO 8601 date and time, for example `2026-01-15T09:00:00+00:00`. Groups last seen before it are ignored. A value without an offset is read as the server's local time. |

## Jira

Required when `Sync:DryRun` is `false`.

| Setting | Default | Description |
| --- | --- | --- |
| `Jira:BaseUrl` | empty | Address of the Jira Cloud site, for example `https://your-site.atlassian.net`. |
| `Jira:Email` | empty | Account the issues are created with. It becomes the reporter. |
| `Jira:ApiToken` | empty | API token of that account. |
| `Jira:ProjectKey` | empty | Project the issues go to. |
| `Jira:IssueType` | `Bug` | Name of the issue type. It must exist in the project. |
| `Jira:AssigneeAccountId` | empty | Atlassian account id of the assignee. Left unassigned when empty. |
| `Jira:ParentKey` | empty | Key of an epic or parent issue. |
| `Jira:Labels` | `logtriage` | Comma-separated labels added to every issue. Spaces become `-`. |
| `Jira:FingerprintLabelPrefix` | `logtriage-` | Prefix of the label that identifies the error group. Changing it makes existing issues invisible to the duplicate check. |
| `Jira:BrowseUrl` | empty | Base URL used in issue links. Set it when browsers reach Jira at a different address than this service does. |

Give the account permission to browse the project and create issues, plus assign issues if you set an assignee. A dedicated service account keeps automated issues recognisable.

## Issue

| Setting | Default | Description |
| --- | --- | --- |
| `Issue:TitleTemplate` | `[{service}] {exceptionType}: {method} {route}` | Placeholders: `{service}`, `{environment}`, `{exceptionType}`, `{exceptionFullType}`, `{method}`, `{route}`, `{status}`, `{message}`. |
| `Issue:IncludeRequestData` | `false` | Add the query string and body of the latest request, after redaction. |
| `Issue:SensitiveKeys` | `password,passwd,pwd,secret,token,authorization,apikey,api_key,api-key,credential,cookie,session,otp,cvv,cvc` | A request field whose name contains one of these words (case-insensitive) has its value replaced with `***`. |
| `Issue:MaxStackTraceLength` | `8000` | Characters of stack trace kept. |
| `Issue:MaxRequestBodyLength` | `4000` | Characters of request body kept. |

## API

| Setting | Default | Description |
| --- | --- | --- |
| `Api:Key` | empty | Shared key expected in the `X-Api-Key` header. While empty, `/api` answers 404. |

Endpoints:

- `GET /healthz` returns 200 while the process runs. It needs no key.
- `GET /api/groups?minutes=60` returns the error groups of the last N minutes (1 to 10080) with their counts, whether they qualify for an issue, and the issue once one is known. Each call queries Loki.

The API has no TLS and no user accounts. Keep it on an internal network or behind your own gateway.
