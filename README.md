<div align="center">
  <img src="https://raw.githubusercontent.com/greyhands2/snerdmq/main/assets/snerdmq-transparent.png" width="200" alt="SnerdMQ Logo"/>
  <h1>SnerdMQ .NET SDK (v0.3.1)</h1>
</div>


## Features
- **Zero Configuration**: No connection strings, no ports, no firewall rules.
- **Native Task Parallelism**: Leverages C#'s massive `async`/`Task` ThreadPool.
- **ASP.NET Core Friendly**: Never blocks the main event loop.
- **Bulletproof Durability**: Uses OS-level file locking for ACID compliance.

## ✨ v0.3.1 AI Features
- **Smart API Rate-Limiting**: Natively tracks `rateLimitGroup` execution velocity to prevent 429 "Too Many Requests" API errors.
- **Payload-Hashing Deduplication**: Automatically computes cryptographic hashes to drop duplicate tasks instantly.
- **Dynamic Float Prioritization**: A native Binary Max-Heap bypasses standard FIFO rules for high urgency tasks.

### ⚙️ Advanced Task Configuration (v0.3.1)
To power complex AI workflows, tasks can now be configured with advanced orchestration parameters:

* **`autoDedupe` (`bool`)**: If set to `true`, the daemon computes a cryptographic hash of the `taskType` and `data`. If an identical payload is currently sitting in the queue pending execution, this new task is silently dropped. Excellent for preventing duplicate generative AI requests from trigger-happy users!
* **`urgencyScore` (`double`)**: A value (e.g. `0.99`) used to bypass the standard FIFO queue. SnerdMQ uses a true Binary Max-Heap to continually float tasks with the highest urgency score to the very front of the execution line. Standard tasks default to `0.0`.
* **`rateLimitGroup` (`string`)**: A custom string (e.g. `"openai_api"` or `"db_writes"`) that groups tasks together for backpressure control.
* **`maxPerMinute` (`int`)**: Used in conjunction with `rateLimitGroup`. If the queue processes more tasks in this group than the allowed limit within a 60-second rolling window, further tasks in this group are temporarily paused. This natively prevents 429 "Too Many Requests" errors when bursting third-party APIs.
* **`executeAt` (`DateTime`)**: A timestamp of when the job should be executed in the future.
* **`cron` (`string`)**: A cron expression (e.g. `"0 * * * *"`) for recurring jobs. Shorthands like `"2h"` or `"10m"` are also supported.
* **`webhookUrl` (`string`)**: By providing a webhook URL, SnerdMQ will completely bypass your local C# handlers and dispatch the task payload via an HTTP POST request directly to the specified URL.

### 🌐 HTTP Webhooks (Serverless Execution)
You can configure a task to execute externally via an HTTP POST request. By setting a `webhookUrl`, the internal background processor will skip any registered handlers (`queue.RegisterHandler`) and directly invoke the HTTP endpoint.

If the HTTP endpoint returns a non-200 status code, it triggers a retry. If it permanently fails (reaches `maxRetries`), the Dead Letter Queue event is automatically fired via a final HTTP POST to the same `webhookUrl` but with the header `X-SnerdMQ-Event: MaxRetriesReached`.

### 🕒 Cron Jobs vs. Retryable Jobs
When using the new scheduling features, it is important to understand the difference between Cron and Retry behaviors:
> - **A Cron Job** is a *Repeatable Job* that executes again **only after a success**, on a fixed schedule.
> - **A Retryable Job** is a *Recovery Job* that executes again **only after a failure**, attempting to recover using the `retryAfterHours` backoff.
> - **Combined:** If a Cron Job fails, it temporarily uses `retryAfterHours` to retry until it recovers. Once it succeeds, it goes back to ticking on its standard cron schedule!

## Installation
*(Coming soon to NuGet)*
```bash
dotnet add package SnerdMQ
```

## Quick Start
```csharp
using SnerdMQ;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Initialize the Queue Orchestrator
        using var queue = new SnerdQueue();

        // 2. Register async job handlers
        queue.RegisterHandler("send_email", async (jsonData) =>
        {
            Console.WriteLine($"Sending email with data: {jsonData}");
            await Task.Delay(1000); // Simulate network request
        });

        // 3. Start listening for jobs in the background ThreadPool
        queue.StartListening();

        // 4. Enqueue a persistent background job!
        queue.Enqueue(
            taskId: "email_123",
            taskType: "send_email",
            jsonData: "{\"user\":\"john.wick@example.com\"}",
            maxRetries: 3,
            retryAfterHours: 0.0,
            rateLimitGroup: "sendgrid_api",
            maxPerMinute: 100,
            autoDedupe: true,
            urgencyScore: 9.5,
            executeAt: null,
            cron: "1h", // Runs every 1 hour!
            webhookUrl: "https://api.example.com/webhook" // Execute via HTTP instead of local handlers
        );

        // Prevent console app from exiting
        await Task.Delay(-1);
    }
}
```

## How it works
This SDK spawns a highly-optimized Rust binary as a child process and communicates with it asynchronously over standard I/O pipes. The Rust engine handles all the complex file-locking, retries, and persistence, while invoking your C# delegates natively!

### ☠️ Dead Letter Queue (Handling Permanent Failures)

When a task fails repeatedly and exhausts its `maxRetries`, the SnerdMQ daemon permanently moves it to the Dead Letter Queue. You can hook into this event to alert your team, update your database, or send a Slack message by registering a Max Retry Handler.

```csharp
// 5. Catch tasks that have permanently failed (Dead Letter Queue)
queue.RegisterMaxRetryHandler("send_email", (data) => {
    Console.WriteLine($"Email task failed after all retries! Data: {data}");
});
```

---

## 🌍 Advanced: Distributed Scaling

By default, the SDK spins up the Rust daemon which writes the queue to a local file (`.snerdata/tasks/tasks.log`). 

If you have multiple ASP.NET Core servers running behind a load balancer and want them to share the exact same queue, simply mount a **Shared Network Drive** (like AWS EFS or NFS) to all of your servers and pass the shared path into the `SnerdQueue` constructor:

```csharp
// All of your C# servers point to the exact same shared file!
// SnerdMQ's native OS file-locking guarantees zero data corruption.
using var queue = new SnerdQueue(null, "/mnt/aws-efs-shared-drive/snerd_tasks.log");
```

*Built with ❤️ for John Wick tier engineering.*
