<div align="center">
  <img src="https://raw.githubusercontent.com/greyhands2/snerdmq/main/assets/snerdmq-transparent.png" width="200" alt="SnerdMQ Logo"/>
  <h1>SnerdMQ .NET SDK (v0.2.1)</h1>
</div>


## Features
- **Zero Configuration**: No connection strings, no ports, no firewall rules.
- **Native Task Parallelism**: Leverages C#'s massive `async`/`Task` ThreadPool.
- **ASP.NET Core Friendly**: Never blocks the main event loop.
- **Bulletproof Durability**: Uses OS-level file locking for ACID compliance.

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
            urgencyScore: 9.5
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
