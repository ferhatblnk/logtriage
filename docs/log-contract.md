# The log contract

LogTriage does not care which language or logging library produced a line. It needs to find a handful of fields in it.

## Fields

| Field | Default name | Required | Used for |
| --- | --- | --- | --- |
| Exception type | `exception.type` | yes | Grouping, title. A line without it is skipped. |
| HTTP method | `http.request.method` | no | Grouping, title. |
| Route template | `http.route` | no | Grouping, title. `/orders/{id}`, not `/orders/41`. |
| Request path | `url.path` | no | Fallback for the route; shown with the latest request. |
| Status code | `http.response.status_code` | no | Grouping and the status filter. |
| Message | `exception.message` | no | Issue description. |
| Stack trace | `exception.stacktrace` | no | Issue description. |
| Query string | `url.query` | no | Latest request, when request data is included. |
| Request body | `http.request.body` | no | Latest request, when request data is included. |

Two stream labels complete the picture: `service_name` for the service and `env` for the environment. Loki assigns `service_name` to every stream by itself, from labels such as `service`, `app` or `container`.

The names follow the [OpenTelemetry semantic conventions](https://opentelemetry.io/docs/specs/semconv/), except `http.request.body`, which has no standard name. All of them can be changed under [`Fields`](configuration.md#fields).

## Where a field is looked up

For each field, in this order:

1. inside the object named by `Fields:Root`, when one is configured;
2. in the JSON line itself;
3. in the stream labels, with every character that is not a letter or digit replaced by `_`.

In JSON, a name matches a flat key or a path through nested objects. These three lines are equivalent:

```json
{"exception.type": "System.TimeoutException"}
{"exception": {"type": "System.TimeoutException"}}
{"log": {"exception.type": "System.TimeoutException"}}
```

The last one needs `Fields:ExceptionType=log.exception.type`, or `Fields:Root=log`.

## Common setups

### ASP.NET Core with the reference middleware

[ErrorLoggingMiddleware.cs](../samples/SampleApi/ErrorLoggingMiddleware.cs) logs every unhandled exception through `ILogger` as one structured record with exactly the fields above. It depends on nothing but ASP.NET Core, so it works with whatever logging provider and shipper you already use. Register it early in the pipeline:

```csharp
app.UseMiddleware<ErrorLoggingMiddleware>();
```

The sample reads the request body so the demo can show redaction. Remove that field if you do not want request bodies in your logs at all.

### .NET JSON console logging

The built-in JSON console formatter (`Logging:Console:FormatterName=json`) nests the fields of a record under `State`:

```json
{"EventId":0,"LogLevel":"Error","Category":"…","Message":"…","State":{"exception.type":"System.TimeoutException","http.route":"/payments"}}
```

Set one option and the defaults work:

```
Fields__Root=State
```

### Fields as Loki structured metadata

If your pipeline attaches the fields to a line as structured metadata instead of putting them in the line, nothing needs configuring. Loki returns structured metadata as labels, with `.` replaced by `_`, and step 3 above finds `exception_type`, `http_route` and the rest. The line itself can then be plain text. Adjust `Loki:Query` so it selects those lines, for example:

```
{service_name=~`.+`} | exception_type != ``
```

### Other languages

Emit the same fields with your logger's structured logging. A minimal line is enough to get grouping and a useful title:

```json
{"exception.type":"java.lang.IllegalStateException","exception.message":"cart is empty","http.request.method":"POST","http.route":"/carts/{id}/checkout","http.response.status_code":500}
```

## Recommendations

- **Log the route template.** Deriving it from the path only recognises numeric, GUID and long hexadecimal identifiers.
- **Log one record per failed request.** If a framework logs the same exception again further up the stack, the occurrence counts double.
- **Keep secrets out of exception messages.** Messages and stack traces are forwarded as logged; only request data is redacted.
- **Give every service its own `service_name`.** It is part of the group identity and the first thing in the issue title.
